import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { json, mockFetch, problem, resetCookies, type Handler } from '../test/fetch-mock'
import { renderApp } from '../test/renderApp'
import { signedInRoutes } from '../test/signedIn'

const civic = {
  id: '0198a1c2-0000-7000-8000-0000000000c1',
  displayName: 'Civic EK',
  make: 'Honda',
  model: 'Civic',
  modelYear: 1998,
  plate: 'ABC1D23',
  currentOdometer: 215000,
  coverPhotoId: '0198a1c2-0000-7000-8000-0000000000f1',
  totalCost: 7043.8,
  lastRecordDate: '2026-10-02',
}

const gol = {
  id: '0198a1c2-0000-7000-8000-0000000000c2',
  displayName: 'Volkswagen Gol 2019',
  make: 'Volkswagen',
  model: 'Gol',
  modelYear: 2019,
  plate: null,
  currentOdometer: 64300,
  coverPhotoId: null,
  totalCost: 1465.7,
  lastRecordDate: '2026-07-14',
}

const status = (state: string, kmRemaining: number | null, daysRemaining: number | null) => ({
  state,
  kmRemaining,
  daysRemaining,
  odometerUpdatedAt: '2026-10-10T12:00:00+00:00',
})

const dueReminders = [
  { vehicleId: gol.id, vehicleName: gol.displayName, reminderId: 'r2', title: 'Revisão de 60 mil', status: status('dueSoon', 800, 12) },
  { vehicleId: civic.id, vehicleName: civic.displayName, reminderId: 'r1', title: 'Troca de óleo', status: status('overdue', -300, null) },
]

const months = (costs: Record<string, number>) =>
  Array.from({ length: 12 }, (_, index) => {
    const month = `2026-${String(index + 1).padStart(2, '0')}`
    return { month, cost: costs[month] ?? 0 }
  })

const recent = (title: string, vehicleId: string, vehicleName: string, date: string, totalCost: number, id: string) => ({
  id,
  vehicleId,
  vehicleName,
  title,
  date,
  totalCost,
})

const summary = {
  year: 2026,
  yearCost: 2586.6,
  yearRecordCount: 4,
  yearVehicleCount: 2,
  byMonth: months({ '2026-03': 206.1, '2026-06': 270.9, '2026-07': 1259.6, '2026-10': 850 }),
  recentRecords: [
    recent('Repintura do para-choque dianteiro', civic.id, 'Civic EK', '2026-10-02', 850, 'rec1'),
    recent('Troca dos quatro pneus', gol.id, 'Volkswagen Gol 2019', '2026-07-14', 1259.6, 'rec2'),
    recent('Troca de óleo e filtro', civic.id, 'Civic EK', '2026-06-04', 270.9, 'rec3'),
    recent('Troca de óleo e filtro', gol.id, 'Volkswagen Gol 2019', '2026-03-26', 206.1, 'rec4'),
  ],
  recordCount: 8,
  firstRecordDate: '2024-11-11',
}

function fakeApi(routes: Record<string, Handler> = {}) {
  return mockFetch({
    ...signedInRoutes(),
    'GET /api/vehicles': () => json([]),
    'GET /api/reminders/due': () => json([]),
    'GET /api/garage/summary': () => json(summary),
    ...routes,
  })
}

const count = (calls: { method: string; path: string }[], method: string, path: string) =>
  calls.filter((call) => call.method === method && call.path === path).length

beforeEach(() => {
  resetCookies()
})

describe('painel sem veículos', () => {
  it('mostra o estado vazio com "Cadastrar veículo" e "Carregar dados de exemplo" bem à vista', async () => {
    fakeApi()
    renderApp('/')

    expect(await screen.findByText('Sua garagem está vazia')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Cadastrar veículo' })).toHaveAttribute('href', '/vehicles/new')
    expect(screen.getByRole('button', { name: 'Carregar dados de exemplo' })).toBeEnabled()
    // Sem veículos a ação principal é a do estado vazio; o "Novo veículo" do cabeçalho só aparece com veículos.
    expect(screen.queryByRole('link', { name: 'Novo veículo' })).not.toBeInTheDocument()
  })

  it('carregar os dados de exemplo busca veículos e lembretes de novo e o painel enche na hora', async () => {
    let seeded = false
    const calls = fakeApi({
      // Voltam fora de ordem de propósito: a tela ordena pelo nome.
      'GET /api/vehicles': () => json(seeded ? [gol, civic] : []),
      'GET /api/reminders/due': () => json(seeded ? dueReminders : []),
      'POST /api/demo/seed': () => {
        seeded = true
        return json({ vehicles: 2, records: 8, reminders: 5 }, 201)
      },
    })
    renderApp('/')
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Carregar dados de exemplo' }))

    expect(await screen.findByRole('heading', { level: 3, name: 'Civic EK' })).toBeInTheDocument()
    expect(await screen.findByRole('heading', { level: 2, name: 'Pede atenção' })).toBeInTheDocument()
    const names = screen.getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent)
    expect(names).toEqual(['Civic EK', 'Volkswagen Gol 2019'])
    expect(screen.queryByText('Sua garagem está vazia')).not.toBeInTheDocument()
    expect(count(calls, 'POST', '/api/demo/seed')).toBe(1)
    // Lista de veículos: a de antes (vazia) e a de depois do exemplo.
    expect(count(calls, 'GET', '/api/vehicles')).toBe(2)

    // Os lembretes foram pedidos de novo depois do exemplo e já vieram preenchidos.
    const seedAt = calls.findIndex((call) => call.method === 'POST' && call.path === '/api/demo/seed')
    const dueAt = calls.findLastIndex((call) => call.path === '/api/reminders/due')
    expect(dueAt).toBeGreaterThan(seedAt)
    expect(await screen.findByText('Troca de óleo')).toBeInTheDocument()
  })

  it('o botão de exemplo fica desabilitado enquanto carrega (sem envio em dobro)', async () => {
    let release: () => void = () => undefined
    const calls = fakeApi({
      'POST /api/demo/seed': () =>
        new Promise<Response>((resolve) => {
          release = () => resolve(json({ vehicles: 2, records: 8, reminders: 5 }, 201))
        }),
    })
    renderApp('/')
    const typist = userEvent.setup()

    const button = await screen.findByRole('button', { name: 'Carregar dados de exemplo' })
    await typist.click(button)
    await waitFor(() => expect(button).toBeDisabled())
    await typist.click(button)
    expect(count(calls, 'POST', '/api/demo/seed')).toBe(1)

    release()
    await waitFor(() => expect(button).toBeEnabled())
  })

  it('ambiente sem dados de exemplo (404) explica em vez de falhar calado', async () => {
    fakeApi({ 'POST /api/demo/seed': () => problem(404, { title: 'Not Found' }) })
    renderApp('/')

    await userEvent.click(await screen.findByRole('button', { name: 'Carregar dados de exemplo' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Os dados de exemplo não estão ligados neste ambiente.')
  })

  it('conta que já tem veículos (409) mostra o aviso da API e atualiza a lista', async () => {
    let vehicles: unknown[] = []
    const calls = fakeApi({
      'GET /api/vehicles': () => json(vehicles),
      'POST /api/demo/seed': () => {
        vehicles = [civic] // outra aba cadastrou antes
        return problem(409, {
          code: 'demo.already_has_data',
          detail: 'Você já tem veículos cadastrados; os dados de exemplo são só para contas vazias.',
        })
      },
    })
    renderApp('/')

    await userEvent.click(await screen.findByRole('button', { name: 'Carregar dados de exemplo' }))

    // A lista foi buscada de novo e já mostra o veículo; nada de "alterado em outra tela" (isso é do 409 de edição).
    expect(await screen.findByRole('heading', { level: 3, name: 'Civic EK' })).toBeInTheDocument()
    expect(count(calls, 'GET', '/api/vehicles')).toBe(2)
  })
})

describe('painel com veículos', () => {
  it('título, gasto do ano, registros e os contadores de lembretes', async () => {
    fakeApi({
      'GET /api/vehicles': () => json([civic, gol]),
      'GET /api/reminders/due': () => json(dueReminders),
    })
    renderApp('/')

    expect(await screen.findByRole('heading', { level: 1, name: 'Sua garagem' })).toBeInTheDocument()
    const spent = (await screen.findByText('Gasto em 2026')).parentElement!
    expect(spent).toHaveTextContent('R$ 2.586,60')
    expect(spent).toHaveTextContent('4 serviços em 2 veículos')
    const records = screen.getByText('Registros').parentElement!
    expect(records).toHaveTextContent('8')
    expect(records).toHaveTextContent('desde nov/2024')
    const overdue = (await screen.findByText('Vencidos')).parentElement!
    expect(overdue).toHaveTextContent('1')
    expect(overdue).toHaveTextContent('Troca de óleo · Civic EK')
    const soon = screen.getByText('Vencem em breve').parentElement!
    expect(soon).toHaveTextContent('1')
    expect(screen.getByRole('link', { name: 'Novo veículo' })).toHaveAttribute('href', '/vehicles/new')
  })

  it('cada veículo vira um card com capa (ou o convite para pôr uma), placa, km, gasto e último serviço', async () => {
    fakeApi({ 'GET /api/vehicles': () => json([civic, gol]) })
    renderApp('/')

    const card = (await screen.findByRole('heading', { level: 3, name: 'Civic EK' })).closest('a')!
    expect(card).toHaveAttribute('href', `/vehicles/${civic.id}`)
    expect(within(card).getByRole('img', { name: 'Civic EK' })).toHaveAttribute(
      'src',
      `/api/vehicles/${civic.id}/attachments/${civic.coverPhotoId}/content`,
    )
    expect(within(card).getByText('Placa ABC1D23')).toBeInTheDocument()
    expect(within(card).getByText('215.000 km')).toBeInTheDocument()
    expect(within(card).getByText('R$ 7.043,80')).toBeInTheDocument()
    expect(within(card).getByText('02/10/2026')).toBeInTheDocument()
    expect(within(card).getByText('Honda Civic 1998')).toBeInTheDocument()

    const golCard = screen.getByRole('heading', { level: 3, name: 'Volkswagen Gol 2019' }).closest('a')!
    expect(within(golCard).getByText('Adicionar foto de capa')).toBeInTheDocument()
    expect(within(golCard).queryByRole('img')).not.toBeInTheDocument()
    expect(within(golCard).getByText('64.300 km')).toBeInTheDocument()
  })

  it('o gráfico do ano diz em texto o gasto de cada mês, e os últimos serviços vêm em ordem', async () => {
    fakeApi({ 'GET /api/vehicles': () => json([civic, gol]) })
    renderApp('/')

    const chart = await screen.findByRole('figure', { name: /Gastos por mês em 2026/ })
    expect(chart.getAttribute('aria-label')).toContain('julho: R$ 1.259,60')
    expect(chart.getAttribute('aria-label')).toContain('demais meses sem gastos')

    const latest = screen.getByRole('heading', { name: 'Últimos serviços' }).closest('section')!
    const items = within(latest).getAllByRole('listitem')
    expect(items).toHaveLength(4)
    expect(items[0]).toHaveTextContent('Repintura do para-choque dianteiro')
    expect(items[0]).toHaveTextContent('Civic EK · 02/10/2026')
    expect(items[0]).toHaveTextContent('R$ 850,00')
    expect(within(items[0]).getByRole('link')).toHaveAttribute('href', `/vehicles/${civic.id}/records/rec1`)
  })

  it('"Pede atenção" lista os vencidos antes dos próximos, com o que falta ou passou e o estado por escrito', async () => {
    fakeApi({
      'GET /api/vehicles': () => json([civic, gol]),
      'GET /api/reminders/due': () => json(dueReminders),
    })
    renderApp('/')

    const section = (await screen.findByRole('heading', { name: 'Pede atenção' })).closest('section')!
    const rows = within(section).getAllByRole('listitem').map((item) => within(item).getByRole('link'))

    expect(rows.map((row) => row.dataset.state)).toEqual(['overdue', 'dueSoon'])
    expect(rows[0]).toHaveTextContent('Troca de óleo')
    expect(rows[0]).toHaveTextContent('Civic EK · 300 km atrasado')
    expect(within(rows[0]).getByText('Vencido')).toBeInTheDocument()
    expect(rows[1]).toHaveTextContent('Revisão de 60 mil')
    expect(rows[1]).toHaveTextContent('Volkswagen Gol 2019 · faltam 800 km · faltam 12 dias')
    expect(within(rows[1]).getByText('Próximo')).toBeInTheDocument()
    expect(rows[0]).toHaveAttribute('href', `/vehicles/${civic.id}`)
    expect(within(section).getByRole('link', { name: 'Ver lembretes' })).toHaveAttribute('href', '/reminders')
  })

  it('sem lembretes vencidos nem próximos diz que está tudo em dia', async () => {
    fakeApi({ 'GET /api/vehicles': () => json([civic]) })
    renderApp('/')

    expect(await screen.findByText('Tudo em dia: nenhum lembrete vencido ou próximo.')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Pede atenção' })).not.toBeInTheDocument()
  })

  it('falha nos lembretes avisa e deixa tentar de novo, sem esconder os veículos', async () => {
    let failing = true
    fakeApi({
      'GET /api/vehicles': () => json([civic]),
      'GET /api/reminders/due': () => (failing ? problem(500, { detail: 'Erro interno.' }) : json(dueReminders.slice(1))),
    })
    renderApp('/')
    const typist = userEvent.setup()

    expect(await screen.findByText('Não foi possível carregar os lembretes.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 3, name: 'Civic EK' })).toBeInTheDocument()

    failing = false
    await typist.click(screen.getByRole('button', { name: 'Tentar de novo' }))
    expect(await screen.findByText('Troca de óleo')).toBeInTheDocument()
  })

  it('falha nos veículos mostra o aviso da API e deixa tentar de novo', async () => {
    let failing = true
    fakeApi({ 'GET /api/vehicles': () => (failing ? problem(500, { detail: 'Erro interno.' }) : json([civic])) })
    renderApp('/')

    expect(await screen.findByRole('alert')).toHaveTextContent('Erro interno.')
    failing = false
    await userEvent.click(screen.getByRole('button', { name: 'Tentar de novo' }))
    expect(await screen.findByRole('heading', { level: 3, name: 'Civic EK' })).toBeInTheDocument()
  })
})
