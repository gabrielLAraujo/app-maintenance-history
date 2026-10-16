import { screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { json, mockFetch, resetCookies, type Handler } from '../../test/fetch-mock'
import { renderApp } from '../../test/renderApp'
import { emptyVehicleSummary, signedInRoutes, trailingMonths, vehicleSummaryRoute } from '../../test/signedIn'

const id = '0198a1c2-0000-7000-8000-0000000000c1'

const civicDto = {
  id,
  type: 'car',
  make: 'Honda',
  model: 'Civic',
  version: 'Sedan LX 1.6 16V Mec. 4p',
  generation: 'EK',
  manufactureYear: 1998,
  modelYear: 1998,
  fuel: 'gasoline',
  nickname: 'Civic EK',
  plate: 'ABC1D23',
  vin: null,
  notes: null,
  currentOdometer: 152300,
  odometerUpdatedAt: '2026-10-12T01:30:00+00:00',
  catalog: null,
  coverPhotoId: null,
  rowVersion: 7,
}

const summary = {
  ...emptyVehicleSummary,
  totalCost: 6883.8,
  maintenance: { cost: 2393.8, count: 4 },
  modification: { cost: 4490, count: 1 },
  firstRecordDate: '2024-11-11',
  kmTracked: 23400,
  costPerKm: 0.29,
  byCategory: [
    { category: 'suspension', cost: 4490 },
    { category: 'engine', cost: 1275.8 },
    { category: 'bodyPaint', cost: 850 },
    { category: 'brakes', cost: 268 },
  ],
  byMonth: trailingMonths({ '2025-12': 4490, '2026-06': 270.9, '2026-10': 850 }),
}

function fakeApi(routes: Record<string, Handler> = {}) {
  return mockFetch({
    ...signedInRoutes(),
    [`GET /api/vehicles/${id}`]: () => json(civicDto),
    ...vehicleSummaryRoute(id, summary),
    ...routes,
  })
}

beforeEach(() => {
  resetCookies()
})

describe('aba Gastos', () => {
  it('categorias na ordem do resumo, cada uma com o rótulo e o valor por escrito', async () => {
    fakeApi()
    renderApp(`/vehicles/${id}?tab=spending`)

    expect(await screen.findByRole('tab', { name: 'Gastos' })).toHaveAttribute('aria-selected', 'true')
    const byCategory = (await screen.findByRole('heading', { name: 'Por categoria' })).closest('section')!
    const rows = within(byCategory).getAllByRole('listitem')
    expect(rows.map((row) => row.textContent)).toEqual([
      'SuspensãoR$ 4.490,00',
      'MotorR$ 1.275,80',
      'Carroceria/PinturaR$ 850,00',
      'FreiosR$ 268,00',
    ])
  })

  it('o gráfico dos últimos 12 meses diz o gasto de cada mês por escrito', async () => {
    fakeApi()
    renderApp(`/vehicles/${id}?tab=spending`)

    const chart = await screen.findByRole('figure', { name: /Gastos por mês nos últimos 12 meses/ })
    const label = chart.getAttribute('aria-label')!
    expect(label).toContain('dezembro: R$ 4.490,00')
    expect(label).toContain('junho: R$ 270,90')
    expect(label).toContain('outubro: R$ 850,00')
    expect(label).toContain('demais meses sem gastos')
  })

  it('sem registros, diz que ainda não há gastos', async () => {
    fakeApi(vehicleSummaryRoute(id, emptyVehicleSummary))
    renderApp(`/vehicles/${id}?tab=spending`)

    expect(await screen.findByText('Nenhum gasto registrado ainda')).toBeInTheDocument()
  })
})
