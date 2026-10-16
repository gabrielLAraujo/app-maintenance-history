import { act, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { api } from '../api/client'
import { json, mockFetch, noContent, problem, resetCookies } from '../test/fetch-mock'
import { renderApp } from '../test/renderApp'

const account = { id: '0198a1c2-0000-7000-8000-000000000001', email: 'ana@example.com' }
const password = 'senha-com-10-chars'

type Route = () => Response | Promise<Response>

/** API falsa com uma sessão: login/cadastro abrem, logout fecha, e as rotas protegidas dão 401 sem ela. */
function fakeApi(
  options: {
    signedIn?: boolean
    registrationEnabled?: boolean
    demoEnabled?: boolean
    login?: Route
    register?: Route
    extra?: Record<string, Route>
  } = {},
) {
  const session = { signedIn: options.signedIn ?? false }
  const open: Route = () => {
    session.signedIn = true
    return noContent()
  }

  const calls = mockFetch({
    'GET /api/auth/antiforgery': () => {
      document.cookie = 'XSRF-TOKEN=test-token; path=/'
      return noContent()
    },
    'GET /api/auth/config': () =>
      json({ registrationEnabled: options.registrationEnabled ?? false, demoEnabled: options.demoEnabled ?? false }),
    'GET /api/auth/me': () => (session.signedIn ? json(account) : problem(401, { title: 'Unauthorized' })),
    'POST /api/auth/login': options.login ?? open,
    'POST /api/auth/register': options.register ?? (() => {
      session.signedIn = true
      return json({ id: account.id }, 201)
    }),
    'POST /api/auth/logout': () => {
      session.signedIn = false
      return noContent()
    },
    'GET /api/vehicles': () => (session.signedIn ? json([]) : problem(401, { title: 'Unauthorized' })),
    'GET /api/reminders/due': () => (session.signedIn ? json([]) : problem(401, { title: 'Unauthorized' })),
    ...options.extra,
  })

  return { calls, session }
}

async function fillCredentials(email = account.email, secret = password) {
  const typist = userEvent.setup()
  await typist.type(await screen.findByLabelText('E-mail'), email)
  await typist.type(screen.getByLabelText('Senha'), secret)
  return typist
}

beforeEach(() => {
  resetCookies()
})

describe('rotas e sessão', () => {
  it('visitante sem sessão em / vê a landing (com spinner, não tela em branco, enquanto confere)', async () => {
    fakeApi()
    renderApp('/')

    expect(screen.getByText('Carregando')).toBeInTheDocument()
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Tudo o que já foi feito no seu carro, num só lugar.' }),
    ).toBeInTheDocument()
  })

  it('visitante sem sessão numa tela logada é levado ao login', async () => {
    fakeApi()
    const { router } = renderApp('/vehicles/x')

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })

  it('o login oferece "Ver demo" quando a demo está ligada', async () => {
    fakeApi({ demoEnabled: true })
    renderApp('/login')

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Ver demo/ })).toBeInTheDocument()
  })

  it('sem a demo, o login não oferece "Ver demo"', async () => {
    fakeApi({ demoEnabled: false })
    renderApp('/login')

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Ver demo/ })).not.toBeInTheDocument()
  })

  it('quem já tem sessão abre direto o painel, com e-mail e Sair no cabeçalho', async () => {
    fakeApi({ signedIn: true })
    renderApp('/')

    expect(await screen.findByRole('heading', { name: 'Sua garagem' })).toBeInTheDocument()
    expect(screen.getByText(account.email)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sair' })).toBeInTheDocument()
  })

  it('quem já tem sessão é mandado de /login para o painel', async () => {
    fakeApi({ signedIn: true })

    renderApp('/login')
    expect(await screen.findByRole('heading', { name: 'Sua garagem' })).toBeInTheDocument()
  })

  it('/register com o cadastro fechado volta ao login e esconde o link "Criar conta"', async () => {
    fakeApi({ registrationEnabled: false })
    renderApp('/register')

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Criar conta' })).not.toBeInTheDocument()
  })

  it('/register com o cadastro aberto mostra o formulário, e o login oferece o link', async () => {
    fakeApi({ registrationEnabled: true })
    renderApp('/register')

    expect(await screen.findByRole('heading', { name: 'Criar conta' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('link', { name: 'Entrar' }))
    expect(await screen.findByRole('link', { name: 'Criar conta' })).toBeInTheDocument()
  })

  it('a página pública abre sem login, sem ir para /login e sem consultar a sessão', async () => {
    const view = { make: 'Honda', model: 'Civic', version: null, generation: null, manufactureYear: 1998, modelYear: 1998, fuel: 'gasoline', nickname: null, plate: null, currentOdometer: 215000, includesCosts: false, records: [], nextCursor: null }
    const { calls } = fakeApi({ extra: { 'GET /api/public/um-token': () => json(view) } })
    const { router } = renderApp('/p/um-token')

    expect(await screen.findByRole('heading', { level: 1, name: 'Honda Civic 1998' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/p/um-token')
    // Nem /api/auth/me, nem antiforgery, nem nada logado: só a rota pública.
    expect(calls.map((call) => call.path)).toEqual(['/api/public/um-token'])
  })

  it('o link público com 401 da API continua na página (nada de /login)', async () => {
    fakeApi({ extra: { 'GET /api/public/um-token': () => problem(401, { title: 'Unauthorized' }) } })
    const { router } = renderApp('/p/um-token')

    expect(await screen.findByRole('heading', { name: 'Link não encontrado ou expirado' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/p/um-token')
  })

  it('rota desconhecida mostra "Página não encontrada"', async () => {
    fakeApi({ signedIn: true })
    renderApp('/nao-existe')

    expect(await screen.findByRole('heading', { name: 'Página não encontrada' })).toBeInTheDocument()
  })
})

describe('login', () => {
  it('entra, busca o usuário e mostra o painel', async () => {
    const { calls } = fakeApi()
    renderApp('/login')

    const typist = await fillCredentials()
    await typist.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('heading', { name: 'Sua garagem' })).toBeInTheDocument()
    expect(screen.getByText(account.email)).toBeInTheDocument()
    const login = calls.find((call) => call.path === '/api/auth/login')
    expect(JSON.parse(login!.body)).toEqual({ email: account.email, password })
    expect(login!.headers.get('X-XSRF-TOKEN')).toBe('test-token')
  })

  it('o botão fica desabilitado enquanto a requisição está em andamento (sem envio em dobro)', async () => {
    let release!: () => void
    const gate = new Promise<void>((resolve) => (release = resolve))
    const { calls, session } = fakeApi({
      login: async () => {
        await gate
        session.signedIn = true
        return noContent()
      },
    })
    renderApp('/login')

    const typist = await fillCredentials()
    await typist.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeDisabled()
    await typist.click(screen.getByRole('button', { name: 'Entrar' }))
    release()

    expect(await screen.findByRole('heading', { name: 'Sua garagem' })).toBeInTheDocument()
    expect(calls.filter((call) => call.path === '/api/auth/login')).toHaveLength(1)
  })

  it('senha errada mostra o aviso da API e continua no login, com o botão liberado', async () => {
    const { calls } = fakeApi({
      login: () => problem(401, { code: 'auth.invalid_credentials', detail: 'E-mail ou senha inválidos.' }),
    })
    renderApp('/login')

    const typist = await fillCredentials()
    await typist.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha inválidos.')
    expect(screen.getByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeEnabled()
    // 401 do login não é "sessão expirada": nada de recarregar o usuário nem de laço.
    expect(calls.filter((call) => call.path === '/api/auth/me')).toHaveLength(1)
  })

  it('valida e-mail e senha (mínimo de 10) antes de chamar a API', async () => {
    const { calls } = fakeApi()
    renderApp('/login')

    const typist = await fillCredentials('ana', 'curta')
    await typist.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('Informe um e-mail válido.')).toBeInTheDocument()
    expect(screen.getByText('A senha deve ter pelo menos 10 caracteres.')).toBeInTheDocument()
    expect(calls.some((call) => call.method === 'POST')).toBe(false)
  })

  it('API fora do ar: avisa da conexão e não trava o botão', async () => {
    fakeApi({
      login: () => {
        throw new TypeError('Failed to fetch')
      },
    })
    renderApp('/login')

    const typist = await fillCredentials()
    await typist.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível falar com o servidor')
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeEnabled()
  })
})

describe('cadastro', () => {
  it('cria a conta, já entra e mostra o painel', async () => {
    const { calls } = fakeApi({ registrationEnabled: true })
    renderApp('/register')

    const typist = await fillCredentials()
    await typist.click(screen.getByRole('button', { name: 'Criar conta' }))

    expect(await screen.findByRole('heading', { name: 'Sua garagem' })).toBeInTheDocument()
    expect(calls.some((call) => call.method === 'POST' && call.path === '/api/auth/register')).toBe(true)
  })

  it('erro de validação da API vai para o campo, sem aviso geral', async () => {
    fakeApi({
      registrationEnabled: true,
      register: () =>
        problem(400, {
          code: 'validation.failed',
          detail: 'Um ou mais erros de validação.',
          errors: { email: ['E-mail já cadastrado.'] },
        }),
    })
    renderApp('/register')

    const typist = await fillCredentials()
    await typist.click(screen.getByRole('button', { name: 'Criar conta' }))

    const message = await screen.findByText('E-mail já cadastrado.')
    expect(screen.getByLabelText('E-mail')).toHaveAccessibleDescription('E-mail já cadastrado.')
    expect(message).toBeInTheDocument()
    expect(screen.queryByText('Um ou mais erros de validação.')).not.toBeInTheDocument()
  })
})

describe('sair e sessão expirada', () => {
  it('Sair encerra a sessão, renova o token e volta ao login', async () => {
    const { calls } = fakeApi({ signedIn: true })
    renderApp('/')

    await userEvent.click(await screen.findByRole('button', { name: 'Sair' }))

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    const logout = calls.findIndex((call) => call.path === '/api/auth/logout')
    expect(calls[logout].headers.get('X-XSRF-TOKEN')).toBe('test-token')
    expect(calls[logout + 1].path).toBe('/api/auth/antiforgery')
  })

  it('401 no meio do uso encerra a sessão (no início, volta a landing), sem tela em branco e sem laço', async () => {
    const { calls, session } = fakeApi({ signedIn: true })
    renderApp('/')
    expect(await screen.findByRole('heading', { name: 'Sua garagem' })).toBeInTheDocument()

    session.signedIn = false // o cookie venceu (ou o deploy trocou as chaves)
    await act(async () => {
      await api.GET('/api/vehicles')
    })

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Tudo o que já foi feito no seu carro, num só lugar.' }),
    ).toBeInTheDocument()
    expect(calls.filter((call) => call.path === '/api/auth/me')).toHaveLength(1)
    // Uma do painel ao abrir e uma deste teste: o 401 não faz o painel (já desmontado) buscar de novo.
    expect(calls.filter((call) => call.path === '/api/vehicles')).toHaveLength(2)
  })

  it('depois de expirar, entrar de novo volta ao painel', async () => {
    const { session } = fakeApi({ signedIn: true })
    renderApp('/')
    await screen.findByRole('heading', { name: 'Sua garagem' })

    session.signedIn = false
    await act(async () => {
      await api.GET('/api/vehicles')
    })
    await userEvent.click((await screen.findAllByRole('link', { name: 'Entrar' }))[0])
    const typist = await fillCredentials()
    await typist.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('heading', { name: 'Sua garagem' })).toBeInTheDocument()
  })
})
