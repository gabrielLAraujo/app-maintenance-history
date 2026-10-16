import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { json, mockFetch, noContent, problem, resetCookies, type Handler } from '../test/fetch-mock'
import { renderApp } from '../test/renderApp'
import { account, signedInRoutes } from '../test/signedIn'

const status = (state: string) => ({
  state,
  kmRemaining: state === 'overdue' ? -300 : 800,
  daysRemaining: null,
  odometerUpdatedAt: '2026-10-10T12:00:00+00:00',
})

const due = [
  { vehicleId: 'v1', vehicleName: 'Civic EK', reminderId: 'r1', title: 'Fluido de freio', status: status('overdue') },
  { vehicleId: 'v1', vehicleName: 'Civic EK', reminderId: 'r2', title: 'Troca de óleo', status: status('dueSoon') },
]

function fakeApi(routes: Record<string, Handler> = {}) {
  return mockFetch({
    ...signedInRoutes(),
    'GET /api/vehicles': () => json([]),
    'GET /api/reminders/due': () => json(due),
    ...routes,
  })
}

beforeEach(() => {
  resetCookies()
})

describe('menu do app', () => {
  it('leva ao painel, aos veículos e aos lembretes, com o painel marcado como atual', async () => {
    fakeApi()
    renderApp('/')

    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    expect(within(nav).getByRole('link', { name: 'Painel' })).toHaveAttribute('href', '/')
    expect(within(nav).getByRole('link', { name: 'Painel' })).toHaveAttribute('aria-current', 'page')
    expect(within(nav).getByRole('link', { name: 'Veículos' })).toHaveAttribute('href', '/vehicles')
    expect(within(nav).getByRole('link', { name: /^Lembretes/ })).toHaveAttribute('href', '/reminders')
  })

  it('o contador dos lembretes diz quantos venceram', async () => {
    fakeApi()
    renderApp('/')

    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    expect(await within(nav).findByRole('link', { name: 'Lembretes, 1 vencido' })).toBeInTheDocument()
  })

  it('sem vencidos, o link dos lembretes não tem contador', async () => {
    fakeApi({ 'GET /api/reminders/due': () => json([due[1]]) })
    renderApp('/')

    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    expect(within(nav).getByRole('link', { name: 'Lembretes' })).toBeInTheDocument()
  })

  it('conta comum: e-mail no rodapé do menu e nenhuma faixa de demonstração', async () => {
    fakeApi()
    renderApp('/')

    expect(await screen.findByText(account.email)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sair' })).toBeInTheDocument()
    expect(screen.queryByText(/Você está na demonstração/)).not.toBeInTheDocument()
  })

  it('o botão Menu abre e fecha a navegação no celular', async () => {
    fakeApi()
    renderApp('/')

    const toggle = await screen.findByRole('button', { name: 'Menu' })
    expect(toggle).toHaveAttribute('aria-expanded', 'false')
    await userEvent.click(toggle)
    expect(toggle).toHaveAttribute('aria-expanded', 'true')
    await userEvent.click(toggle)
    expect(toggle).toHaveAttribute('aria-expanded', 'false')
  })
})

describe('conta demo', () => {
  const demoAccount = () => ({
    id: '0198a1c2-0000-7000-8000-0000000000d1',
    email: 'demo-0198a1c200007000800000000000d1@demo.garagem.invalid',
    isDemo: true,
    // 22 h 59 min daqui: a faixa arredonda para cima.
    demoExpiresAt: new Date(Date.now() + (22 * 60 + 59) * 60_000).toISOString(),
  })

  it('mostra a faixa com as horas restantes e "Visitante" no lugar do e-mail', async () => {
    const demo = demoAccount()
    fakeApi({ 'GET /api/auth/me': () => json(demo) })
    renderApp('/')

    const banner = await screen.findByText(/Você está na demonstração/)
    expect(banner).toHaveTextContent('Você está na demonstração. Pode mexer à vontade: esta conta é apagada em 23 h.')
    expect(banner.closest('[role="status"]')).not.toBeNull()
    expect(screen.getByRole('link', { name: 'Ver o código' })).toHaveAttribute(
      'href',
      'https://github.com/gabrielLAraujo/app-maintenance-history',
    )
    expect(screen.getByText('Visitante')).toBeInTheDocument()
    // O e-mail técnico da conta demo nunca aparece.
    expect(screen.queryByText(demo.email)).not.toBeInTheDocument()
    expect(document.body.textContent).not.toContain('demo.garagem.invalid')
  })

  it('a demo que sai volta para a landing, não para o login', async () => {
    const demo = demoAccount()
    let loggedOut = false
    fakeApi({
      'GET /api/auth/me': () => (loggedOut ? problem(401, { status: 401 }) : json(demo)),
      'POST /api/auth/logout': () => {
        loggedOut = true
        return noContent()
      },
    })
    const { router } = renderApp('/vehicles')

    await userEvent.click(await screen.findByRole('button', { name: 'Sair' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(await screen.findByRole('heading', { level: 1, name: /Tudo o que já foi feito no seu carro/ })).toBeInTheDocument()
  })

  it('a conta comum que sai vai para o login', async () => {
    let loggedOut = false
    fakeApi({
      'GET /api/auth/me': () => (loggedOut ? problem(401, { status: 401 }) : json(account)),
      'POST /api/auth/logout': () => {
        loggedOut = true
        return noContent()
      },
    })
    const { router } = renderApp('/vehicles')

    await userEvent.click(await screen.findByRole('button', { name: 'Sair' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
  })
})
