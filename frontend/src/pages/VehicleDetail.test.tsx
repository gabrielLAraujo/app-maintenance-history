import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { json, mockFetch, problem, resetCookies, type Call, type Handler } from '../test/fetch-mock'
import { renderApp } from '../test/renderApp'
import { signedInRoutes, trailingMonths, vehicleSummaryRoute } from '../test/signedIn'

const id = '0198a1c2-0000-7000-8000-0000000000c1'
const recordsPath = `/api/vehicles/${id}/records`
const fipePath = `/api/vehicles/${id}/fipe-price`

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
  currentOdometer: 215000,
  odometerUpdatedAt: '2026-10-12T01:30:00+00:00',
  catalog: null,
  rowVersion: 7,
}

const linkedCivic = { ...civicDto, catalog: { type: 'car', fipeCode: '005340-6', fipeYearCode: '1998-1' } }

const coverId = '0198a1c2-0000-7000-8000-0000000000f1'

/** O resumo do Civic de exemplo (6.883,80 em cinco registros, 23.400 km acompanhados). */
const civicSummary = {
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
  lastRecord: { id: 'rec5', title: 'Repintura do para-choque dianteiro', date: '2026-10-02' },
}

/** Um registro da lista, com valores de um Civic de verdade; `extra` troca o que o teste quer destacar. */
const item = (n: number, extra: Record<string, unknown> = {}) => ({
  id: `0198a1c2-0000-7000-8000-00000000a${String(n).padStart(3, '0')}`,
  type: 'maintenance',
  date: '2026-09-20',
  odometer: 214000,
  title: `Registro ${n}`,
  category: 'engine',
  totalCost: 0,
  attachmentCount: 0,
  ...extra,
})

const oil = item(1, { title: 'Troca de óleo e filtro', totalCost: 189.9, attachmentCount: 1 })
const coilover = item(2, {
  type: 'modification',
  title: 'Coilover Tein Street',
  date: '2025-08-12',
  odometer: 150000,
  category: 'suspension',
  totalCost: 4550,
  attachmentCount: 3,
})

function fakeApi(routes: Record<string, Handler> = {}) {
  return mockFetch({
    ...signedInRoutes(),
    'GET /api/vehicles': () => json([]),
    'GET /api/reminders/due': () => json([]),
    [`GET /api/vehicles/${id}`]: () => json(civicDto),
    [`GET ${recordsPath}`]: () => json({ items: [oil, coilover], nextCursor: null }),
    // As outras abas (a F5 trouxe os painéis reais): aqui só importa que existam e abram.
    [`GET /api/vehicles/${id}/reminders`]: () => json([]),
    [`GET /api/vehicles/${id}/share-links`]: () => json([]),
    ...vehicleSummaryRoute(id, civicSummary),
    ...routes,
  })
}

/** Os parâmetros de query de uma chamada (o fetch falso guarda caminho + query juntos). */
const queryOf = (call: Call) => new URL(call.path, 'http://localhost').searchParams

const listCalls = (calls: Call[]) => calls.filter((call) => call.method === 'GET' && call.path.startsWith(recordsPath))

const historyList = () => screen.findByRole('list')

beforeEach(() => {
  resetCookies()
})

describe('histórico do veículo', () => {
  it('lista do mais novo para o mais antigo (como a API manda), com título, data, km, categoria, total e anexos', async () => {
    fakeApi()
    renderApp(`/vehicles/${id}`)

    const list = await historyList()
    const rows = within(list).getAllByRole('listitem')
    expect(rows).toHaveLength(2)

    const first = rows[0]
    expect(within(first).getByRole('heading', { level: 3, name: 'Troca de óleo e filtro' })).toBeInTheDocument()
    // Linha do tempo: data e km em mono, à esquerda do cartão do serviço.
    expect(within(first).getByText('20/09/2026')).toHaveClass('font-mono')
    expect(within(first).getByText('214.000 km')).toBeInTheDocument()
    expect(within(first).getByText('Manutenção')).toBeInTheDocument()
    expect(within(first).getByText('Motor')).toBeInTheDocument()
    expect(within(first).getByText('R$ 189,90')).toBeInTheDocument()
    expect(within(first).getByText('1 anexo')).toBeInTheDocument()
    expect(within(first).getByRole('link')).toHaveAttribute('href', `/vehicles/${id}/records/${oil.id}`)

    const second = rows[1]
    expect(within(second).getByText('Modificação')).toBeInTheDocument()
    expect(within(second).getByText('Suspensão')).toBeInTheDocument()
    expect(within(second).getByText('R$ 4.550,00')).toBeInTheDocument()
    expect(within(second).getByText('3 anexos')).toBeInTheDocument()
    expect(within(second).getByText(/12\/08\/2025/)).toBeInTheDocument()

    expect(screen.getByRole('link', { name: 'Novo registro' })).toHaveAttribute('href', `/vehicles/${id}/records/new`)
  })

  it('registro sem km, sem custo e sem anexo mostra só a data (sem zeros nem "0 anexos")', async () => {
    fakeApi({
      [`GET ${recordsPath}`]: () => json({ items: [item(3, { odometer: null, totalCost: 0, attachmentCount: 0 })], nextCursor: null }),
    })
    renderApp(`/vehicles/${id}`)

    const row = within(await historyList()).getByRole('listitem')
    expect(within(row).getByText('20/09/2026')).toBeInTheDocument()
    expect(within(row).queryByText(/km/)).not.toBeInTheDocument()
    expect(within(row).queryByText(/R\$/)).not.toBeInTheDocument()
    expect(within(row).queryByText(/anexo/)).not.toBeInTheDocument()
  })

  it('sem nenhum registro mostra o convite para registrar o primeiro serviço', async () => {
    fakeApi({ [`GET ${recordsPath}`]: () => json({ items: [], nextCursor: null }) })
    renderApp(`/vehicles/${id}`)

    expect(await screen.findByText('Nenhum registro ainda')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Registrar o primeiro serviço' })).toHaveAttribute(
      'href',
      `/vehicles/${id}/records/new`,
    )
    // Sem registros ainda, filtro não faz sentido.
    expect(screen.queryByLabelText('Tipo')).not.toBeInTheDocument()
  })

  it('filtra por tipo e categoria: a API recebe os filtros e eles ficam no endereço', async () => {
    const calls = fakeApi({
      [`GET ${recordsPath}`]: (call) => {
        const params = queryOf(call)
        return json({ items: params.get('type') === 'modification' ? [coilover] : [oil, coilover], nextCursor: null })
      },
    })
    const { router } = renderApp(`/vehicles/${id}`)
    const typist = userEvent.setup()

    await historyList()
    expect(queryOf(listCalls(calls)[0]).has('type')).toBe(false)

    await typist.selectOptions(screen.getByLabelText('Tipo'), 'Modificação')

    await waitFor(() => expect(within(screen.getByRole('list')).getAllByRole('listitem')).toHaveLength(1))
    expect(within(screen.getByRole('list')).getByText('Coilover Tein Street')).toBeInTheDocument()
    expect(queryOf(listCalls(calls).at(-1)!).get('type')).toBe('modification')
    expect(router.state.location.search).toBe('?type=modification')

    await typist.selectOptions(screen.getByLabelText('Categoria'), 'Suspensão')
    await waitFor(() => expect(queryOf(listCalls(calls).at(-1)!).get('category')).toBe('suspension'))
    const last = queryOf(listCalls(calls).at(-1)!)
    expect(last.get('type')).toBe('modification') // o filtro de tipo continua
    expect(router.state.location.search).toBe('?type=modification&category=suspension')
  })

  it('abre já filtrado quando o endereço traz os filtros; valor desconhecido vale como "sem filtro"', async () => {
    const calls = fakeApi()
    renderApp(`/vehicles/${id}?type=modification&category=inexistente`)

    await historyList()
    expect(screen.getByLabelText('Tipo')).toHaveValue('modification')
    expect(screen.getByLabelText('Categoria')).toHaveValue('')
    const params = queryOf(listCalls(calls)[0])
    expect(params.get('type')).toBe('modification')
    expect(params.has('category')).toBe(false)
  })

  it('filtro sem resultado diz isso e "Limpar filtros" volta à lista inteira', async () => {
    const calls = fakeApi({
      [`GET ${recordsPath}`]: (call) =>
        json({ items: queryOf(call).has('category') ? [] : [oil, coilover], nextCursor: null }),
    })
    const { router } = renderApp(`/vehicles/${id}?category=brakes`)
    const typist = userEvent.setup()

    expect(await screen.findByText('Nenhum registro com esses filtros.')).toBeInTheDocument()
    // Com filtro ativo, a tela não vira o convite de "primeiro registro": os filtros continuam à mostra.
    expect(screen.getByLabelText('Categoria')).toHaveValue('brakes')
    expect(screen.queryByText('Nenhum registro ainda')).not.toBeInTheDocument()

    await typist.click(screen.getByRole('button', { name: 'Limpar filtros' }))

    expect(await screen.findByText('Troca de óleo e filtro')).toBeInTheDocument()
    expect(router.state.location.search).toBe('')
    expect(queryOf(listCalls(calls).at(-1)!).has('category')).toBe(false)
  })

  it('"Carregar mais" segue o nextCursor e acrescenta a página; some quando acaba', async () => {
    const calls = fakeApi({
      [`GET ${recordsPath}`]: (call) =>
        queryOf(call).get('cursor') === 'pagina-2'
          ? json({ items: [coilover], nextCursor: null })
          : json({ items: [oil], nextCursor: 'pagina-2' }),
    })
    renderApp(`/vehicles/${id}`)
    const typist = userEvent.setup()

    expect(await screen.findByText('Troca de óleo e filtro')).toBeInTheDocument()
    expect(screen.queryByText('Coilover Tein Street')).not.toBeInTheDocument()
    expect(queryOf(listCalls(calls)[0]).has('cursor')).toBe(false)

    await typist.click(screen.getByRole('button', { name: 'Carregar mais' }))

    expect(await screen.findByText('Coilover Tein Street')).toBeInTheDocument()
    expect(screen.getByText('Troca de óleo e filtro')).toBeInTheDocument() // a primeira página continua
    expect(queryOf(listCalls(calls).at(-1)!).get('cursor')).toBe('pagina-2')
    expect(screen.queryByRole('button', { name: 'Carregar mais' })).not.toBeInTheDocument()
    expect(within(screen.getByRole('list')).getAllByRole('listitem')).toHaveLength(2)
  })

  it('erro ao buscar o histórico mostra o motivo e "Tentar de novo" busca outra vez', async () => {
    let attempt = 0
    fakeApi({
      [`GET ${recordsPath}`]: () => {
        attempt += 1
        return attempt === 1
          ? problem(500, { code: 'server.error', detail: 'Não foi possível carregar o histórico agora.' })
          : json({ items: [oil], nextCursor: null })
      },
    })
    renderApp(`/vehicles/${id}`)
    const typist = userEvent.setup()

    expect(await screen.findByText('Não foi possível carregar o histórico agora.')).toBeInTheDocument()
    // O cabeçalho do veículo continua inteiro: o erro é só do histórico.
    expect(screen.getByRole('heading', { level: 1, name: 'Civic EK' })).toBeInTheDocument()

    await typist.click(screen.getByRole('button', { name: 'Tentar de novo' }))

    expect(await screen.findByText('Troca de óleo e filtro')).toBeInTheDocument()
    expect(screen.queryByText('Não foi possível carregar o histórico agora.')).not.toBeInTheDocument()
  })
})

describe('abas do veículo', () => {
  it('quatro abas, Histórico aberta; clicar em outra troca o painel e põe a aba no endereço', async () => {
    fakeApi()
    const { router } = renderApp(`/vehicles/${id}`)
    const typist = userEvent.setup()

    const tabs = await screen.findAllByRole('tab')
    expect(tabs.map((tab) => tab.textContent)).toEqual(['Histórico', 'Lembretes', 'Gastos', 'Compartilhar'])
    expect(screen.getByRole('tab', { name: 'Histórico' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('tablist')).toHaveAccessibleName('Seções do veículo')
    expect(screen.getByRole('tabpanel')).toHaveAccessibleName('Histórico')

    await typist.click(screen.getByRole('tab', { name: 'Lembretes' }))

    expect(screen.getByRole('tab', { name: 'Lembretes' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('tab', { name: 'Histórico' })).toHaveAttribute('aria-selected', 'false')
    expect(screen.getByRole('tabpanel')).toHaveAccessibleName('Lembretes')
    expect(screen.queryByText('Troca de óleo e filtro')).not.toBeInTheDocument()
    expect(router.state.location.search).toBe('?tab=reminders')

    await typist.click(screen.getByRole('tab', { name: 'Histórico' }))
    expect(router.state.location.search).toBe('') // a aba de abertura não polui o endereço
    expect(await screen.findByText('Troca de óleo e filtro')).toBeInTheDocument()
  })

  it('o endereço escolhe a aba ao abrir; nome de aba desconhecido cai no Histórico', async () => {
    fakeApi()
    const { router } = renderApp(`/vehicles/${id}?tab=share`)

    expect(await screen.findByRole('tab', { name: 'Compartilhar' })).toHaveAttribute('aria-selected', 'true')
    await router.navigate(`/vehicles/${id}?tab=nada`)
    // A navegação do router é uma transição do React: a tela troca de aba um instante depois de `navigate` voltar.
    await waitFor(() => expect(screen.getByRole('tab', { name: 'Histórico' })).toHaveAttribute('aria-selected', 'true'))
  })

  it('setas, Home e End trocam de aba (e o foco vai junto); só a aba aberta entra na ordem do Tab', async () => {
    fakeApi()
    renderApp(`/vehicles/${id}`)
    const typist = userEvent.setup()

    const history = await screen.findByRole('tab', { name: 'Histórico' })
    const reminders = screen.getByRole('tab', { name: 'Lembretes' })
    const share = screen.getByRole('tab', { name: 'Compartilhar' })
    expect(history).toHaveAttribute('tabindex', '0')
    expect(reminders).toHaveAttribute('tabindex', '-1')

    history.focus()
    await typist.keyboard('{ArrowRight}')
    expect(reminders).toHaveAttribute('aria-selected', 'true')
    expect(reminders).toHaveFocus()
    expect(reminders).toHaveAttribute('tabindex', '0')

    await typist.keyboard('{End}')
    expect(share).toHaveAttribute('aria-selected', 'true')
    expect(share).toHaveFocus()

    await typist.keyboard('{ArrowRight}') // dá a volta
    expect(history).toHaveAttribute('aria-selected', 'true')

    await typist.keyboard('{ArrowLeft}') // e volta para o fim
    expect(share).toHaveAttribute('aria-selected', 'true')

    await typist.keyboard('{Home}')
    expect(history).toHaveAttribute('aria-selected', 'true')
    expect(history).toHaveFocus()
  })

  it('trocar de aba descarta os filtros do histórico', async () => {
    fakeApi()
    const { router } = renderApp(`/vehicles/${id}?type=modification&category=suspension`)
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('tab', { name: 'Compartilhar' }))
    expect(router.state.location.search).toBe('?tab=share')
  })
})

describe('cabeçalho: km e valor FIPE', () => {
  it('"Corrigir km digitada errado" abre a janela já no modo de correção', async () => {
    fakeApi()
    renderApp(`/vehicles/${id}`)
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Corrigir km digitada errado' }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByRole('heading', { name: 'Corrigir km' })).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Km correta')).toHaveValue('215000')
    expect(within(dialog).getByRole('button', { name: 'Voltar para atualizar a km' })).toBeInTheDocument()
  })

  it('"Atualizar km" abre a janela no modo normal', async () => {
    fakeApi()
    renderApp(`/vehicles/${id}`)
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Atualizar km' }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByRole('button', { name: 'Salvar km' })).toBeInTheDocument()
  })

  it('veículo vinculado à FIPE mostra o valor de tabela e o mês de referência', async () => {
    fakeApi({
      [`GET /api/vehicles/${id}`]: () => json(linkedCivic),
      [`GET ${fipePath}`]: () => json({ price: 23456.78, referenceMonth: 'outubro de 2026' }),
    })
    renderApp(`/vehicles/${id}`)

    expect(await screen.findByText('Tabela FIPE')).toBeInTheDocument()
    expect(await screen.findByText('R$ 23.456,78')).toBeInTheDocument()
    expect(screen.getByText('referência outubro de 2026')).toBeInTheDocument()
  })

  it('veículo sem vínculo não consulta a FIPE nem mostra a linha', async () => {
    const calls = fakeApi({ [`GET ${fipePath}`]: () => json({ price: 1, referenceMonth: 'x' }) })
    renderApp(`/vehicles/${id}`)

    await historyList()
    expect(screen.queryByText('Tabela FIPE')).not.toBeInTheDocument()
    expect(calls.filter((call) => call.path === fipePath)).toHaveLength(0)
  })

  it('FIPE fora do ar (503): a tela segue inteira, com o aviso e "Tentar de novo"', async () => {
    let attempt = 0
    const calls = fakeApi({
      [`GET /api/vehicles/${id}`]: () => json(linkedCivic),
      [`GET ${fipePath}`]: () => {
        attempt += 1
        return attempt === 1
          ? problem(503, { code: 'catalog.unavailable', detail: 'Catálogo FIPE indisponível no momento.' })
          : json({ price: 23456.78, referenceMonth: 'outubro de 2026' })
      },
    })
    renderApp(`/vehicles/${id}`)
    const typist = userEvent.setup()

    expect(await screen.findByText('Valor indisponível agora.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Civic EK' })).toBeInTheDocument()
    expect(screen.getByText('215.000 km')).toBeInTheDocument()
    expect(await historyList()).toBeInTheDocument()

    await typist.click(screen.getByRole('button', { name: 'Tentar de novo' }))

    expect(await screen.findByText('R$ 23.456,78')).toBeInTheDocument()
    expect(screen.queryByText('Valor indisponível agora.')).not.toBeInTheDocument()
    expect(calls.filter((call) => call.path === fipePath)).toHaveLength(2)
  })

  it('vínculo que a API não resolve (404 vehicle.catalog_not_linked) também não vira tela de erro', async () => {
    fakeApi({
      [`GET /api/vehicles/${id}`]: () => json(linkedCivic),
      [`GET ${fipePath}`]: () =>
        problem(404, { code: 'vehicle.catalog_not_linked', detail: 'Este veículo não está vinculado à tabela FIPE.' }),
    })
    renderApp(`/vehicles/${id}`)

    expect(await screen.findByText('Valor indisponível agora.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Civic EK' })).toBeInTheDocument()
    expect(await historyList()).toBeInTheDocument()
    expect(screen.queryByText('Veículo não encontrado')).not.toBeInTheDocument()
  })
})

describe('cabeçalho: capa e indicadores', () => {
  it('com capa, mostra a foto do veículo', async () => {
    fakeApi({ [`GET /api/vehicles/${id}`]: () => json({ ...civicDto, coverPhotoId: coverId }) })
    renderApp(`/vehicles/${id}`)

    expect(await screen.findByRole('img', { name: 'Civic EK' })).toHaveAttribute(
      'src',
      `/api/vehicles/${id}/attachments/${coverId}/content`,
    )
    expect(screen.queryByText('Adicionar foto de capa')).not.toBeInTheDocument()
  })

  it('sem capa, explica como escolher uma foto do histórico (sem link que não leva a lugar nenhum)', async () => {
    fakeApi({ [`GET /api/vehicles/${id}`]: () => json({ ...civicDto, coverPhotoId: null }) })
    renderApp(`/vehicles/${id}`)

    expect(await screen.findByText('Sem foto de capa')).toBeInTheDocument()
    expect(screen.getByText('Abra um registro com foto no histórico e escolha "Usar como capa".')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /foto de capa/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('img', { name: 'Civic EK' })).not.toBeInTheDocument()
  })

  it('mostra gasto total, manutenção, modificações e custo por km do resumo', async () => {
    fakeApi()
    renderApp(`/vehicles/${id}`)

    const numbers = await screen.findByRole('region', { name: 'Gastos do veículo' })
    await within(numbers).findByText('Gasto total')
    const kpi = (label: string) => within(numbers).getByText(label).parentElement!
    expect(kpi('Gasto total')).toHaveTextContent('R$ 6.883,80')
    expect(kpi('Gasto total')).toHaveTextContent('5 registros desde nov/2024')
    expect(kpi('Manutenção')).toHaveTextContent('R$ 2.393,80')
    expect(kpi('Manutenção')).toHaveTextContent('4 serviços')
    expect(kpi('Modificações')).toHaveTextContent('R$ 4.490,00')
    expect(kpi('Modificações')).toHaveTextContent('1 modificação')
    expect(kpi('Custo por km')).toHaveTextContent('R$ 0,29')
    expect(kpi('Custo por km')).toHaveTextContent('em 23.400 km acompanhados')
  })

  it('sem km suficiente, o custo por km fica em "—" e diz a partir de quando aparece', async () => {
    fakeApi(vehicleSummaryRoute(id, { ...civicSummary, kmTracked: 800, costPerKm: null }))
    renderApp(`/vehicles/${id}`)

    const numbers = await screen.findByRole('region', { name: 'Gastos do veículo' })
    const costPerKm = (await within(numbers).findByText('Custo por km')).parentElement!
    expect(costPerKm).toHaveTextContent('—')
    expect(costPerKm).toHaveTextContent('a partir de 1.000 km acompanhados')
  })
})
