import { beforeEach, describe, expect, it, vi } from 'vitest'
import { json, mockFetch, noContent, problem, resetCookies } from '../test/fetch-mock'

// O cliente guarda estado no módulo (token em andamento, handler de 401): cada teste começa com um módulo novo.
async function loadClient() {
  vi.resetModules()
  return import('./client')
}

/** Antiforgery falso: cada GET "grava o cookie" com um token novo, como o Set-Cookie da API. */
function antiforgeryRoute() {
  let issued = 0
  return () => {
    document.cookie = `XSRF-TOKEN=token-${++issued}; path=/`
    return noContent()
  }
}

const credentials = { email: 'ana@example.com', password: 'senha-com-10-chars' }

beforeEach(() => {
  resetCookies()
})

describe('401', () => {
  it('401 dispara onUnauthorized', async () => {
    const { api, onUnauthorized } = await loadClient()
    mockFetch({ 'GET /api/vehicles': () => problem(401, { title: 'Unauthorized' }) })
    const handler = vi.fn()
    onUnauthorized(handler)

    const { response } = await api.GET('/api/vehicles')

    expect(response.status).toBe(401)
    expect(handler).toHaveBeenCalledTimes(1)
  })

  it('401 de login errado e de "ninguém logado" (/me) não é sessão expirada', async () => {
    const { api, onUnauthorized } = await loadClient()
    mockFetch({
      'GET /api/auth/antiforgery': antiforgeryRoute(),
      'POST /api/auth/login': () => problem(401, { code: 'auth.invalid_credentials' }),
      'GET /api/auth/me': () => problem(401, { title: 'Unauthorized' }),
    })
    const handler = vi.fn()
    onUnauthorized(handler)

    await api.POST('/api/auth/login', { body: credentials })
    await api.GET('/api/auth/me')

    expect(handler).not.toHaveBeenCalled()
  })

  it('401 numa rota pública (anônima) nunca é sessão expirada', async () => {
    const { api, onUnauthorized } = await loadClient()
    mockFetch({ 'GET /api/public/abc': () => problem(401, { title: 'Unauthorized' }) })
    const handler = vi.fn()
    onUnauthorized(handler)

    const { response } = await api.GET('/api/public/{token}', { params: { path: { token: 'abc' } } })

    expect(response.status).toBe(401)
    expect(handler).not.toHaveBeenCalled()
  })

  it('o handler registrado por último é o único chamado, e dá para cancelar o registro', async () => {
    const { api, onUnauthorized } = await loadClient()
    mockFetch({ 'GET /api/vehicles': () => problem(401, {}) })
    const first = vi.fn()
    const second = vi.fn()
    onUnauthorized(first)
    const unsubscribe = onUnauthorized(second)

    await api.GET('/api/vehicles')
    expect(first).not.toHaveBeenCalled()
    expect(second).toHaveBeenCalledTimes(1)

    unsubscribe()
    await api.GET('/api/vehicles')
    expect(second).toHaveBeenCalledTimes(1)
  })

  it('sem handler registrado, 401 só devolve a resposta (sem exceção)', async () => {
    const { api } = await loadClient()
    mockFetch({ 'GET /api/vehicles': () => problem(401, {}) })

    const { response } = await api.GET('/api/vehicles')

    expect(response.status).toBe(401)
  })
})

describe('antiforgery', () => {
  it('mutação envia X-XSRF-TOKEN do cookie', async () => {
    const { api } = await loadClient()
    document.cookie = 'XSRF-TOKEN=abc; path=/'
    const calls = mockFetch({
      'GET /api/auth/antiforgery': () => noContent(),
      'POST /api/demo/seed': () => json({}, 201),
    })

    await api.POST('/api/demo/seed')

    const post = calls.find((call) => call.method === 'POST')
    expect(post?.headers.get('X-XSRF-TOKEN')).toBe('abc')
  })

  it('envia o cookie da sessão (credentials include) e só manda o token em escrita', async () => {
    const { api } = await loadClient()
    document.cookie = 'XSRF-TOKEN=abc; path=/'
    const calls = mockFetch({
      'GET /api/auth/antiforgery': () => noContent(),
      'GET /api/vehicles': () => json({ items: [] }),
      'POST /api/demo/seed': () => json({}, 201),
    })

    await api.GET('/api/vehicles')
    await api.POST('/api/demo/seed')

    const get = calls.find((call) => call.path === '/api/vehicles')
    const post = calls.find((call) => call.path === '/api/demo/seed')
    expect(get?.credentials).toBe('include')
    expect(get?.headers.has('X-XSRF-TOKEN')).toBe(false)
    expect(post?.credentials).toBe('include')
  })

  it('busca o token antes da primeira mutação, uma única vez, mesmo com mutações simultâneas', async () => {
    const { api } = await loadClient()
    const calls = mockFetch({
      'GET /api/auth/antiforgery': antiforgeryRoute(),
      'POST /api/demo/seed': () => json({}, 201),
    })

    await Promise.all([api.POST('/api/demo/seed'), api.POST('/api/demo/seed')])
    await api.POST('/api/demo/seed')

    const tokenFetches = calls.filter((call) => call.path === '/api/auth/antiforgery')
    expect(tokenFetches).toHaveLength(1)
    const seeds = calls.filter((call) => call.method === 'POST')
    expect(seeds.map((call) => call.headers.get('X-XSRF-TOKEN'))).toEqual(['token-1', 'token-1', 'token-1'])
    expect(calls[0].path).toBe('/api/auth/antiforgery')
  })

  it('ensureAntiforgery(true) sempre busca um token novo; sem force reaproveita', async () => {
    const { ensureAntiforgery } = await loadClient()
    const calls = mockFetch({ 'GET /api/auth/antiforgery': antiforgeryRoute() })

    await ensureAntiforgery()
    await ensureAntiforgery()
    expect(calls).toHaveLength(1)

    await ensureAntiforgery(true)
    expect(calls).toHaveLength(2)
    expect(document.cookie).toContain('XSRF-TOKEN=token-2')
  })

  it('cookie sumiu depois de carregado: a próxima mutação busca de novo', async () => {
    const { api } = await loadClient()
    const calls = mockFetch({
      'GET /api/auth/antiforgery': antiforgeryRoute(),
      'POST /api/demo/seed': () => json({}, 201),
    })

    await api.POST('/api/demo/seed')
    resetCookies()
    await api.POST('/api/demo/seed')

    expect(calls.filter((call) => call.path === '/api/auth/antiforgery')).toHaveLength(2)
    const seeds = calls.filter((call) => call.method === 'POST')
    expect(seeds[1].headers.get('X-XSRF-TOKEN')).toBe('token-2')
  })

  it('falha ao buscar o token não trava: a mutação segue e a próxima tenta buscar de novo', async () => {
    const { api } = await loadClient()
    let failing = true
    const calls = mockFetch({
      'GET /api/auth/antiforgery': () => {
        if (failing) return problem(503, {})
        document.cookie = 'XSRF-TOKEN=recovered; path=/'
        return noContent()
      },
      'POST /api/demo/seed': () => json({}, 201),
    })

    await api.POST('/api/demo/seed')
    failing = false
    await api.POST('/api/demo/seed')

    const seeds = calls.filter((call) => call.method === 'POST')
    expect(seeds[0].headers.has('X-XSRF-TOKEN')).toBe(false)
    expect(seeds[1].headers.get('X-XSRF-TOKEN')).toBe('recovered')
  })
})

describe('token depois de mudar a identidade', () => {
  // O token do antiforgery é vinculado ao usuário: sem renovar, o primeiro POST depois de logar dá
  // 400 auth.antiforgery_invalid.
  it('login renova o token antes da próxima mutação', async () => {
    const { api } = await loadClient()
    const calls = mockFetch({
      'GET /api/auth/antiforgery': antiforgeryRoute(),
      'POST /api/auth/login': () => noContent(),
      'POST /api/demo/seed': () => json({}, 201),
    })

    await api.POST('/api/auth/login', { body: credentials })
    await api.POST('/api/demo/seed')

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      'GET /api/auth/antiforgery',
      'POST /api/auth/login',
      'GET /api/auth/antiforgery',
      'POST /api/demo/seed',
    ])
    expect(calls[1].headers.get('X-XSRF-TOKEN')).toBe('token-1')
    expect(calls[3].headers.get('X-XSRF-TOKEN')).toBe('token-2')
  })

  it('cadastro (que já abre a sessão) renova o token antes da próxima mutação', async () => {
    const { api } = await loadClient()
    const calls = mockFetch({
      'GET /api/auth/antiforgery': antiforgeryRoute(),
      'POST /api/auth/register': () => json({ id: '0198a1c2-0000-7000-8000-000000000001' }, 201),
      'POST /api/demo/seed': () => json({}, 201),
    })

    await api.POST('/api/auth/register', { body: credentials })
    await api.POST('/api/demo/seed')

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      'GET /api/auth/antiforgery',
      'POST /api/auth/register',
      'GET /api/auth/antiforgery',
      'POST /api/demo/seed',
    ])
    expect(calls[3].headers.get('X-XSRF-TOKEN')).toBe('token-2')
  })

  it('logout renova o token (o próximo é anônimo) antes da próxima mutação', async () => {
    const { api } = await loadClient()
    const calls = mockFetch({
      'GET /api/auth/antiforgery': antiforgeryRoute(),
      'POST /api/auth/logout': () => noContent(),
      'POST /api/auth/login': () => noContent(),
    })

    await api.POST('/api/auth/logout')
    await api.POST('/api/auth/login', { body: credentials })

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      'GET /api/auth/antiforgery',
      'POST /api/auth/logout',
      'GET /api/auth/antiforgery',
      'POST /api/auth/login',
      'GET /api/auth/antiforgery',
    ])
    expect(calls[3].headers.get('X-XSRF-TOKEN')).toBe('token-2')
  })

  it('login que falha não renova o token', async () => {
    const { api } = await loadClient()
    const calls = mockFetch({
      'GET /api/auth/antiforgery': antiforgeryRoute(),
      'POST /api/auth/login': () => problem(401, { code: 'auth.invalid_credentials' }),
    })

    await api.POST('/api/auth/login', { body: credentials })
    await api.POST('/api/auth/login', { body: credentials })

    expect(calls.filter((call) => call.path === '/api/auth/antiforgery')).toHaveLength(1)
  })

  it('a renovação já terminou quando a chamada de login volta (a tela não precisa esperar nada)', async () => {
    const { api } = await loadClient()
    mockFetch({
      'GET /api/auth/antiforgery': antiforgeryRoute(),
      'POST /api/auth/login': () => noContent(),
    })

    await api.POST('/api/auth/login', { body: credentials })

    expect(document.cookie).toContain('XSRF-TOKEN=token-2')
  })
})
