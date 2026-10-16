import { screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { json, mockFetch, resetCookies, type Handler } from '../test/fetch-mock'
import { renderApp } from '../test/renderApp'
import { signedInRoutes } from '../test/signedIn'

const status = (state: string, kmRemaining: number | null, daysRemaining: number | null) => ({
  state,
  kmRemaining,
  daysRemaining,
  odometerUpdatedAt: '2026-10-10T12:00:00+00:00',
})

const reminder = (reminderId: string, title: string, vehicleId: string, vehicleName: string, state: ReturnType<typeof status>) => ({
  vehicleId,
  vehicleName,
  reminderId,
  title,
  status: state,
})

const all = [
  reminder('r1', 'Fluido de freio', 'c1', 'Civic EK', status('overdue', null, -34)),
  reminder('r2', 'Troca de óleo e filtro', 'c1', 'Civic EK', status('dueSoon', 500, 49)),
  reminder('r3', 'Correia dentada', 'c1', 'Civic EK', status('ok', 26600, 800)),
  reminder('r4', 'Troca de óleo e filtro', 'g1', 'Volkswagen Gol 2019', status('ok', 4000, 160)),
]

function fakeApi(routes: Record<string, Handler> = {}) {
  return mockFetch({ ...signedInRoutes(), 'GET /api/reminders': () => json(all), ...routes })
}

beforeEach(() => {
  resetCookies()
})

describe('página Lembretes', () => {
  it('agrupa em Vencidos, Próximos e Em dia, com veículo, situação e link para a aba do veículo', async () => {
    fakeApi()
    renderApp('/reminders')

    expect(await screen.findByRole('heading', { level: 1, name: 'Lembretes' })).toBeInTheDocument()
    const groups = (await screen.findAllByRole('heading', { level: 2 })).map((heading) => heading.textContent)
    expect(groups).toEqual(['Vencidos', 'Próximos', 'Em dia'])

    const overdue = screen.getByRole('heading', { level: 2, name: 'Vencidos' }).closest('section')!
    const [brake] = within(overdue).getAllByRole('listitem')
    expect(brake).toHaveTextContent('Fluido de freio')
    expect(brake).toHaveTextContent('Civic EK · 34 dias atrasado')
    expect(within(brake).getByText('Vencido')).toBeInTheDocument()
    expect(within(brake).getByRole('link')).toHaveAttribute('href', '/vehicles/c1?tab=reminders')

    const ok = screen.getByRole('heading', { level: 2, name: 'Em dia' }).closest('section')!
    expect(within(ok).getAllByRole('listitem')).toHaveLength(2)
    expect(within(ok).getAllByRole('listitem')[1]).toHaveTextContent('Volkswagen Gol 2019 · faltam 4.000 km · faltam 160 dias')
  })

  it('só mostra os grupos que têm lembretes', async () => {
    fakeApi({ 'GET /api/reminders': () => json([all[2]]) })
    renderApp('/reminders')

    const groups = (await screen.findAllByRole('heading', { level: 2 })).map((heading) => heading.textContent)
    expect(groups).toEqual(['Em dia'])
  })

  it('sem lembretes mostra o estado vazio', async () => {
    fakeApi({ 'GET /api/reminders': () => json([]) })
    renderApp('/reminders')

    expect(await screen.findByText('Nenhum lembrete ainda')).toBeInTheDocument()
  })
})
