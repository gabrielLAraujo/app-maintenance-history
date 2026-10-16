import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { dueRemindersKey } from '../../api/hooks/reminders'
import { json, mockFetch, noContent, problem, resetCookies, type Call, type Handler } from '../../test/fetch-mock'
import { renderApp } from '../../test/renderApp'
import { signedInRoutes } from '../../test/signedIn'

// Relógio parado em 12/10/2026 às 12h em Brasília: "hoje", "há 30 dias" e "km atualizada em" ficam determinísticos.
const NOW = new Date('2026-10-12T15:00:00Z')

const id = '0198a1c2-0000-7000-8000-0000000000c1'
const remindersPath = `/api/vehicles/${id}/reminders`

const civic = {
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
  odometerUpdatedAt: '2026-10-11T12:00:00+00:00',
  catalog: null,
  rowVersion: 7,
}

const status = (state: string, kmRemaining: number | null, daysRemaining: number | null, odometerUpdatedAt = civic.odometerUpdatedAt) => ({
  state,
  kmRemaining,
  daysRemaining,
  odometerUpdatedAt,
})

// Os três lembretes do Civic de exemplo: óleo perto de vencer, correia em dia e fluido de freio vencido.
const oil = {
  id: '0198a1c2-0000-7000-8000-00000000b001',
  title: 'Troca de óleo e filtro',
  intervalKm: 10000,
  intervalMonths: 12,
  lastDoneDate: '2026-03-10',
  lastDoneOdometer: 205500,
  status: status('dueSoon', 500, 150),
  rowVersion: 3,
}
const belt = {
  id: '0198a1c2-0000-7000-8000-00000000b002',
  title: 'Correia dentada',
  intervalKm: 60000,
  intervalMonths: 48,
  lastDoneDate: '2025-06-01',
  lastDoneOdometer: 197000,
  status: status('ok', 42000, 900),
  rowVersion: 1,
}
const brake = {
  id: '0198a1c2-0000-7000-8000-00000000b003',
  title: 'Fluido de freio',
  intervalKm: null,
  intervalMonths: 24,
  lastDoneDate: '2024-10-02',
  lastDoneOdometer: null,
  status: status('overdue', null, -10),
  rowVersion: 5,
}

function fakeApi(routes: Record<string, Handler> = {}) {
  return mockFetch({
    ...signedInRoutes(),
    'GET /api/vehicles': () => json([]),
    'GET /api/reminders/due': () => json([]),
    [`GET /api/vehicles/${id}`]: () => json(civic),
    [`GET ${remindersPath}`]: () => json([oil, belt, brake]),
    ...routes,
  })
}

const open = () => renderApp(`/vehicles/${id}?tab=reminders`)
const bodyOf = (call: Call | undefined) => JSON.parse(call?.body ?? 'null')
const writes = (calls: Call[], method: string) => calls.filter((call) => call.method === method && call.path.startsWith(remindersPath))

/** Quantas vezes os lembretes vencidos e próximos (painel e contador do menu) foram buscados. */
const dueFetches = (calls: Call[]) => calls.filter((call) => call.method === 'GET' && call.path === '/api/reminders/due').length

beforeEach(() => {
  resetCookies()
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(NOW)
})

afterEach(() => {
  vi.useRealTimers()
})

describe('lista de lembretes', () => {
  it('mostra o selo de estado e o que falta: "Vencido" há 10 dias, "Próximo" com Faltam 500 km, "Em dia"', async () => {
    fakeApi()
    open()

    // A API manda em qualquer ordem: a tela põe o vencido primeiro, depois o próximo, depois o em dia.
    const rows = await screen.findAllByRole('listitem')
    expect(rows.map((row) => within(row).getByRole('heading', { level: 3 }).textContent)).toEqual([
      'Fluido de freio',
      'Troca de óleo e filtro',
      'Correia dentada',
    ])

    expect(within(rows[0]).getByText('Vencido')).toBeInTheDocument()
    expect(within(rows[0]).getByText(/há 10 dias/)).toBeInTheDocument()
    expect(within(rows[0]).getByText('A cada 24 meses')).toBeInTheDocument()

    expect(within(rows[1]).getByText('Próximo')).toBeInTheDocument()
    expect(within(rows[1]).getByText('Faltam 500 km')).toBeInTheDocument()
    expect(within(rows[1]).getByText('Faltam 150 dias')).toBeInTheDocument()
    expect(within(rows[1]).getByText('A cada 10.000 km ou 12 meses')).toBeInTheDocument()

    expect(within(rows[2]).getByText('Em dia')).toBeInTheDocument()
    expect(within(rows[2]).getByText('Faltam 42.000 km')).toBeInTheDocument()
  })

  it('mostra a base do lembrete: data da última vez e, quando há, a km', async () => {
    fakeApi()
    open()

    const rows = await screen.findAllByRole('listitem')
    expect(within(rows[1]).getByText('Última vez: 10/03/2026 · 205.500 km')).toBeInTheDocument()
    expect(within(rows[0]).getByText('Última vez: 02/10/2024')).toBeInTheDocument()
  })

  it('sem lembretes mostra o convite para criar o primeiro', async () => {
    fakeApi({ [`GET ${remindersPath}`]: () => json([]) })
    open()

    expect(await screen.findByText('Nenhum lembrete ainda')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Criar o primeiro lembrete' })).toBeInTheDocument()
  })

  it('erro ao buscar mostra o motivo e "Tentar de novo" busca outra vez', async () => {
    let attempt = 0
    fakeApi({
      [`GET ${remindersPath}`]: () => {
        attempt += 1
        return attempt === 1
          ? problem(500, { code: 'server.error', detail: 'Não foi possível carregar os lembretes agora.' })
          : json([brake])
      },
    })
    open()
    const typist = userEvent.setup()

    expect(await screen.findByText('Não foi possível carregar os lembretes agora.')).toBeInTheDocument()
    await typist.click(screen.getByRole('button', { name: 'Tentar de novo' }))

    expect(await screen.findByText('Fluido de freio')).toBeInTheDocument()
  })
})

describe('aviso de km desatualizada', () => {
  const stale = { ...civic, odometerUpdatedAt: '2026-08-02T15:00:00+00:00' }
  const staleOil = { ...oil, status: status('dueSoon', 500, 150, stale.odometerUpdatedAt) }

  it('km atualizada há mais de 30 dias avisa a data e oferece atualizar', async () => {
    fakeApi({ [`GET /api/vehicles/${id}`]: () => json(stale), [`GET ${remindersPath}`]: () => json([staleOil]) })
    open()
    const typist = userEvent.setup()

    const notice = await screen.findByRole('alert')
    expect(notice).toHaveTextContent('Km atualizada em 02/08')

    await typist.click(within(notice).getByRole('button', { name: 'Atualizar km' }))
    expect(await screen.findByRole('dialog', { name: 'Atualizar km' })).toBeInTheDocument()
  })

  it('km recente não avisa', async () => {
    fakeApi()
    open()

    await screen.findAllByRole('listitem')
    expect(screen.queryByText(/km atualizada em/i)).not.toBeInTheDocument()
  })

  it('só há lembretes por tempo: a km velha não importa, sem aviso', async () => {
    fakeApi({ [`GET /api/vehicles/${id}`]: () => json(stale), [`GET ${remindersPath}`]: () => json([brake]) })
    open()

    await screen.findAllByRole('listitem')
    expect(screen.queryByText(/km atualizada em/i)).not.toBeInTheDocument()
  })
})

describe('criar lembrete', () => {
  async function openCreate() {
    const typist = userEvent.setup()
    await typist.click(await screen.findByRole('button', { name: 'Novo lembrete' }))
    const dialog = await screen.findByRole('dialog', { name: 'Novo lembrete' })
    return { typist, dialog }
  }

  it('abre com a data de hoje e a km atual do veículo', async () => {
    fakeApi()
    open()

    const { dialog } = await openCreate()

    expect(within(dialog).getByLabelText('Última vez feito em')).toHaveValue('2026-10-12')
    expect(within(dialog).getByLabelText('Km na última vez')).toHaveValue('215000')
    expect(within(dialog).getByLabelText('Intervalo em km')).toHaveValue('')
    expect(within(dialog).getByLabelText('Intervalo em meses')).toHaveValue('')
  })

  it('envia número (não texto), a data e a km base; depois atualiza a lista, o painel e fecha', async () => {
    let created = false
    const calls = fakeApi({
      [`GET ${remindersPath}`]: () => json(created ? [oil, belt, brake, { ...brake, id: 'novo', title: 'Correia de acessórios' }] : [oil, belt, brake]),
      [`POST ${remindersPath}`]: () => {
        created = true
        return json({ id: 'novo' }, 201)
      },
    })
    const { queryClient } = open()
    queryClient.setQueryData(dueRemindersKey, [])
    const { typist, dialog } = await openCreate()

    await typist.type(within(dialog).getByLabelText('Título'), '  Correia de acessórios ')
    await typist.type(within(dialog).getByLabelText('Intervalo em km'), '40.000')
    const dueBefore = dueFetches(calls)
    await typist.click(within(dialog).getByRole('button', { name: 'Criar lembrete' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    const post = writes(calls, 'POST')
    expect(post).toHaveLength(1)
    expect(post[0].headers.get('X-XSRF-TOKEN')).toBe('test-token')
    expect(bodyOf(post[0])).toEqual({
      title: 'Correia de acessórios',
      intervalKm: 40000,
      intervalMonths: null,
      lastDoneDate: '2026-10-12',
      lastDoneOdometer: 215000,
    })

    // A lista foi buscada de novo (o lembrete novo aparece) e os lembretes do painel e do menu também.
    expect(await screen.findByText('Correia de acessórios')).toBeInTheDocument()
    await waitFor(() => expect(dueFetches(calls)).toBeGreaterThan(dueBefore))
  })

  it('só por meses: a km base é opcional e vai como null quando em branco', async () => {
    const calls = fakeApi({ [`POST ${remindersPath}`]: () => json({ id: 'novo' }, 201) })
    open()
    const { typist, dialog } = await openCreate()

    await typist.type(within(dialog).getByLabelText('Título'), 'Alinhamento')
    await typist.type(within(dialog).getByLabelText('Intervalo em meses'), '6')
    await typist.clear(within(dialog).getByLabelText('Km na última vez'))
    fireEvent.change(within(dialog).getByLabelText('Última vez feito em'), { target: { value: '2026-09-01' } })
    await typist.click(within(dialog).getByRole('button', { name: 'Criar lembrete' }))

    await waitFor(() => expect(writes(calls, 'POST')).toHaveLength(1))
    expect(bodyOf(writes(calls, 'POST')[0])).toEqual({
      title: 'Alinhamento',
      intervalKm: null,
      intervalMonths: 6,
      lastDoneDate: '2026-09-01',
      lastDoneOdometer: null,
    })
  })

  it('sem nenhum intervalo recusa, no campo, sem chamar a API', async () => {
    const calls = fakeApi()
    open()
    const { typist, dialog } = await openCreate()

    await typist.type(within(dialog).getByLabelText('Título'), 'Qualquer coisa')
    await typist.click(within(dialog).getByRole('button', { name: 'Criar lembrete' }))

    expect(await within(dialog).findByText('Informe o intervalo em km, em meses ou ambos.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Intervalo em km')).toBeInvalid()
    expect(writes(calls, 'POST')).toHaveLength(0)
  })

  it('intervalo em km sem a km da última vez é recusado (o back-end exige para calcular o que falta)', async () => {
    const calls = fakeApi()
    open()
    const { typist, dialog } = await openCreate()

    await typist.type(within(dialog).getByLabelText('Título'), 'Óleo do câmbio')
    await typist.type(within(dialog).getByLabelText('Intervalo em km'), '30000')
    await typist.clear(within(dialog).getByLabelText('Km na última vez'))
    await typist.click(within(dialog).getByRole('button', { name: 'Criar lembrete' }))

    expect(await within(dialog).findByText('Informe a km da última vez para lembretes por km.')).toBeInTheDocument()
    expect(writes(calls, 'POST')).toHaveLength(0)
  })

  it('valores fora da faixa ou que parecem decimais são recusados (km "187.45", meses 241, data futura)', async () => {
    const calls = fakeApi()
    open()
    const { typist, dialog } = await openCreate()

    await typist.type(within(dialog).getByLabelText('Título'), 'Pastilhas')
    await typist.type(within(dialog).getByLabelText('Intervalo em km'), '187.45')
    await typist.type(within(dialog).getByLabelText('Intervalo em meses'), '241')
    fireEvent.change(within(dialog).getByLabelText('Última vez feito em'), { target: { value: '2026-10-13' } })
    await typist.click(within(dialog).getByRole('button', { name: 'Criar lembrete' }))

    expect(await within(dialog).findByText('Informe o intervalo em km, de 1 a 1.000.000.')).toBeInTheDocument()
    expect(within(dialog).getByText('Informe os meses, de 1 a 240.')).toBeInTheDocument()
    expect(within(dialog).getByText('A data não pode estar no futuro.')).toBeInTheDocument()
    expect(writes(calls, 'POST')).toHaveLength(0)
  })

  it('erro da API na data vai para o campo e a janela continua aberta', async () => {
    fakeApi({
      [`POST ${remindersPath}`]: () =>
        problem(422, { code: 'reminder.base_date_in_future', detail: 'A data da última vez não pode estar no futuro.' }),
    })
    open()
    const { typist, dialog } = await openCreate()

    await typist.type(within(dialog).getByLabelText('Título'), 'Pastilhas')
    await typist.type(within(dialog).getByLabelText('Intervalo em meses'), '12')
    await typist.click(within(dialog).getByRole('button', { name: 'Criar lembrete' }))

    expect(await within(dialog).findByText('A data da última vez não pode estar no futuro.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Última vez feito em')).toBeInvalid()
    expect(screen.getByRole('dialog', { name: 'Novo lembrete' })).toBeInTheDocument()
  })

  it('o botão fica desabilitado enquanto salva (nada de lembrete duplicado)', async () => {
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    const calls = fakeApi({
      [`POST ${remindersPath}`]: async () => {
        await gate
        return json({ id: 'novo' }, 201)
      },
    })
    open()
    const { typist, dialog } = await openCreate()

    await typist.type(within(dialog).getByLabelText('Título'), 'Pastilhas')
    await typist.type(within(dialog).getByLabelText('Intervalo em meses'), '12')
    const submit = within(dialog).getByRole('button', { name: 'Criar lembrete' })
    await typist.click(submit)

    await waitFor(() => expect(submit).toBeDisabled())
    await typist.click(submit)
    release()

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(writes(calls, 'POST')).toHaveLength(1)
  })
})

describe('editar lembrete', () => {
  it('abre com os valores salvos e salva com a rowVersion', async () => {
    const calls = fakeApi({ [`PUT ${remindersPath}/${oil.id}`]: () => noContent() })
    const { queryClient } = open()
    queryClient.setQueryData(dueRemindersKey, [])
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Editar Troca de óleo e filtro' }))
    const dialog = await screen.findByRole('dialog', { name: 'Editar lembrete' })
    expect(within(dialog).getByLabelText('Título')).toHaveValue('Troca de óleo e filtro')
    expect(within(dialog).getByLabelText('Intervalo em km')).toHaveValue('10000')
    expect(within(dialog).getByLabelText('Intervalo em meses')).toHaveValue('12')
    expect(within(dialog).getByLabelText('Última vez feito em')).toHaveValue('2026-03-10')
    expect(within(dialog).getByLabelText('Km na última vez')).toHaveValue('205500')

    await typist.clear(within(dialog).getByLabelText('Intervalo em meses'))
    const dueBefore = dueFetches(calls)
    await typist.click(within(dialog).getByRole('button', { name: 'Salvar alterações' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(bodyOf(writes(calls, 'PUT')[0])).toEqual({
      title: 'Troca de óleo e filtro',
      intervalKm: 10000,
      intervalMonths: null,
      lastDoneDate: '2026-03-10',
      lastDoneOdometer: 205500,
      rowVersion: 3,
    })
    await waitFor(() => expect(dueFetches(calls)).toBeGreaterThan(dueBefore))
  })

  it('alterado em outra tela (409): avisa, busca de novo e o formulário recomeça com o que há no servidor', async () => {
    let fetches = 0
    fakeApi({
      [`GET ${remindersPath}`]: () => {
        fetches += 1
        return json(fetches === 1 ? [brake] : [{ ...brake, title: 'Fluido de freio DOT 4', rowVersion: 6 }])
      },
      [`PUT ${remindersPath}/${brake.id}`]: () =>
        problem(409, { code: 'concurrency.conflict', detail: 'O lembrete foi alterado por outra pessoa.' }),
    })
    open()
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Editar Fluido de freio' }))
    const dialog = await screen.findByRole('dialog', { name: 'Editar lembrete' })
    await typist.click(within(dialog).getByRole('button', { name: 'Salvar alterações' }))

    expect(await within(dialog).findByText(/foi alterado em outra tela/)).toBeInTheDocument()
    await waitFor(() => expect(within(dialog).getByLabelText('Título')).toHaveValue('Fluido de freio DOT 4'))
    expect(screen.getByRole('dialog', { name: 'Editar lembrete' })).toBeInTheDocument()
  })
})

describe('excluir lembrete', () => {
  it('pede confirmação; confirmar apaga e atualiza a lista e o painel', async () => {
    let removed = false
    const calls = fakeApi({
      [`GET ${remindersPath}`]: () => json(removed ? [oil, belt] : [oil, belt, brake]),
      [`DELETE ${remindersPath}/${brake.id}`]: () => {
        removed = true
        return noContent()
      },
    })
    const { queryClient } = open()
    queryClient.setQueryData(dueRemindersKey, [])
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Excluir Fluido de freio' }))
    const dialog = await screen.findByRole('dialog', { name: 'Excluir este lembrete?' })
    expect(within(dialog).getByText('Fluido de freio')).toBeInTheDocument()
    expect(writes(calls, 'DELETE')).toHaveLength(0)

    const dueBefore = dueFetches(calls)
    await typist.click(within(dialog).getByRole('button', { name: 'Excluir de vez' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(writes(calls, 'DELETE')).toHaveLength(1)
    expect(writes(calls, 'DELETE')[0].headers.get('X-XSRF-TOKEN')).toBe('test-token')
    await waitFor(() => expect(screen.queryByText('Fluido de freio')).not.toBeInTheDocument())
    await waitFor(() => expect(dueFetches(calls)).toBeGreaterThan(dueBefore))
  })

  it('cancelar não apaga nada', async () => {
    const calls = fakeApi()
    open()
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Excluir Fluido de freio' }))
    await typist.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancelar' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(writes(calls, 'DELETE')).toHaveLength(0)
    expect(screen.getByText('Fluido de freio')).toBeInTheDocument()
  })

  it('erro ao apagar aparece na janela, que continua aberta', async () => {
    fakeApi({
      [`DELETE ${remindersPath}/${brake.id}`]: () => problem(500, { code: 'server.error', detail: 'Não foi possível excluir agora.' }),
    })
    open()
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Excluir Fluido de freio' }))
    const dialog = await screen.findByRole('dialog')
    await typist.click(within(dialog).getByRole('button', { name: 'Excluir de vez' }))

    expect(await within(dialog).findByText('Não foi possível excluir agora.')).toBeInTheDocument()
  })
})
