import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { json, mockFetch, noContent, problem, resetCookies, type Handler } from '../test/fetch-mock'
import { renderApp } from '../test/renderApp'

const HEADLINE = 'Tudo o que já foi feito no seu carro, num só lugar.'

const demoAccount = {
  id: '0198a1c2-0000-7000-8000-0000000000d1',
  email: 'demo-0198a1c200007000800000000000d1@demo.garagem.invalid',
  isDemo: true,
  demoExpiresAt: '2026-10-17T15:00:00+00:00',
}

/** Visitante sem sessão; `demo` decide o que o POST /api/auth/demo responde. */
function fakeVisitor(options: { demoEnabled?: boolean; demo?: Handler; extra?: Record<string, Handler> } = {}) {
  const session = { signedIn: false }

  const calls = mockFetch({
    'GET /api/auth/antiforgery': () => {
      document.cookie = 'XSRF-TOKEN=test-token; path=/'
      return noContent()
    },
    'GET /api/auth/config': () => json({ registrationEnabled: false, demoEnabled: options.demoEnabled ?? true }),
    'GET /api/auth/me': () => (session.signedIn ? json(demoAccount) : problem(401, { title: 'Unauthorized' })),
    'POST /api/auth/demo':
      options.demo ??
      (() => {
        session.signedIn = true
        return json({ expiresAt: demoAccount.demoExpiresAt }, 201)
      }),
    'GET /api/vehicles': () => json([]),
    'GET /api/reminders/due': () => json([]),
    ...options.extra,
  })

  return { calls, session }
}

beforeEach(() => {
  resetCookies()
})

describe('landing', () => {
  it('visitante em / vê a landing com "Ver demo"', async () => {
    fakeVisitor()
    renderApp('/')

    expect(await screen.findByRole('heading', { level: 1, name: HEADLINE })).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /Ver demo/ }).length).toBeGreaterThan(0)
    expect(screen.getAllByRole('link', { name: 'Entrar' })[0]).toHaveAttribute('href', '/login')
  })

  it('sem demo ligada não há "Ver demo", só "Entrar"', async () => {
    fakeVisitor({ demoEnabled: false })
    renderApp('/')

    expect(await screen.findByRole('heading', { level: 1, name: HEADLINE })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Ver demo/ })).not.toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: 'Entrar' }).length).toBeGreaterThan(0)
  })

  it('Ver demo abre a sessão e leva ao painel', async () => {
    const { calls } = fakeVisitor()
    const { router } = renderApp('/')

    await userEvent.click((await screen.findAllByRole('button', { name: /Ver demo/ }))[0])

    await waitFor(() => expect(screen.queryByRole('heading', { level: 1, name: HEADLINE })).not.toBeInTheDocument())
    expect(router.state.location.pathname).toBe('/')
    const posts = calls.filter((call) => call.method === 'POST' && call.path === '/api/auth/demo')
    expect(posts).toHaveLength(1)
    expect(posts[0].headers.get('X-XSRF-TOKEN')).toBe('test-token')
  })

  it('429 avisa que este endereço já criou demonstrações demais', async () => {
    fakeVisitor({
      demo: () => problem(429, { code: 'rate_limit.exceeded', detail: 'Muitas tentativas. Aguarde um minuto.' }),
    })
    renderApp('/')

    await userEvent.click((await screen.findAllByRole('button', { name: /Ver demo/ }))[0])

    expect(
      await screen.findByText('Muitas demonstrações criadas a partir deste endereço. Tente de novo em alguns minutos.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: HEADLINE })).toBeInTheDocument()
  })

  it('503 avisa que a demonstração está cheia', async () => {
    fakeVisitor({
      demo: () =>
        problem(503, { code: 'demo.unavailable', detail: 'A demonstração está cheia agora. Tente de novo mais tarde.' }),
    })
    renderApp('/')

    await userEvent.click((await screen.findAllByRole('button', { name: /Ver demo/ }))[0])

    expect(await screen.findByText('A demonstração está cheia agora. Tente de novo mais tarde.')).toBeInTheDocument()
  })
})
