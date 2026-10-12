import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { json, mockFetch, noContent, problem, resetCookies, type Call, type Handler } from '../test/fetch-mock'
import { renderApp } from '../test/renderApp'
import { signedInRoutes } from '../test/signedIn'

const id = '0198a1c2-0000-7000-8000-0000000000c1'
const recordId = '0198a1c2-0000-7000-8000-0000000000e1'
const recordsPath = `/api/vehicles/${id}/records`

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

const reminder = (reminderId: string, title: string) => ({
  id: reminderId,
  title,
  intervalKm: 5000,
  intervalMonths: 6,
  lastDoneDate: '2026-04-01',
  lastDoneOdometer: 210000,
  status: { state: 'ok', kmRemaining: 4000, daysRemaining: 120, odometerUpdatedAt: '2026-10-12T01:30:00+00:00' },
  rowVersion: 1,
})

const oilReminder = reminder('0198a1c2-0000-7000-8000-0000000000a1', 'Troca de óleo')
const beltReminder = reminder('0198a1c2-0000-7000-8000-0000000000a2', 'Correia dentada')

const savedRecord = {
  id: recordId,
  type: 'maintenance',
  date: '2026-10-12',
  odometer: 214500,
  title: 'Troca de óleo e filtro',
  description: null,
  category: 'engine',
  performedBy: null,
  laborCost: 150,
  replacedOriginalPart: null,
  parts: [],
  totalCost: 150,
  attachments: [],
  rowVersion: 3,
}

function fakeApi(routes: Record<string, Handler> = {}) {
  return mockFetch({
    ...signedInRoutes(),
    'GET /api/vehicles': () => json([]),
    'GET /api/reminders/due': () => json([]),
    [`GET /api/vehicles/${id}`]: () => json(civicDto),
    [`GET /api/vehicles/${id}/reminders`]: () => json([]),
    [`GET ${recordsPath}/${recordId}`]: () => json(savedRecord),
    ...routes,
  })
}

const sent = (calls: Call[], method: string, path: string) =>
  calls.filter((call) => call.method === method && call.path === path)

const bodyOf = (call: Call | undefined) => JSON.parse(call?.body ?? 'null') as unknown

type Typist = ReturnType<typeof userEvent.setup>

async function addPart(typist: Typist, number: number) {
  await typist.click(await screen.findByRole('button', { name: 'Adicionar peça' }))
  return screen.getByRole('group', { name: `Peça ${number}` })
}

beforeEach(() => {
  resetCookies()
  // "Hoje" fixo (só o Date; os timers seguem reais para o userEvent e o waitFor). 15:00 UTC = meio-dia em Brasília.
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-12T15:00:00Z'))
})

afterEach(() => {
  vi.useRealTimers()
})

describe('novo registro', () => {
  it('monta as peças na ordem informada, com números (não textos), e abre o registro salvo', async () => {
    const calls = fakeApi({ [`POST ${recordsPath}`]: () => json({ id: recordId }, 201) })
    renderApp(`/vehicles/${id}/records/new`)
    const typist = userEvent.setup()

    await typist.type(await screen.findByLabelText('Título'), 'Troca de óleo e filtro')
    await typist.selectOptions(screen.getByLabelText('Categoria'), 'engine')
    await typist.type(screen.getByLabelText('Km (opcional)'), '214.500')
    await typist.type(screen.getByLabelText('Mão de obra (opcional)'), '150')

    const oil = await addPart(typist, 1)
    await typist.type(within(oil).getByLabelText('Nome da peça'), 'Óleo 5W30')
    await typist.type(within(oil).getByLabelText('Marca (opcional)'), 'Motul')
    await typist.clear(within(oil).getByLabelText('Quantidade'))
    await typist.type(within(oil).getByLabelText('Quantidade'), '3,5')
    await typist.selectOptions(within(oil).getByLabelText('Unidade'), 'liter')
    await typist.type(within(oil).getByLabelText('Preço unitário (opcional)'), '45,90')

    const filter = await addPart(typist, 2)
    await typist.type(within(filter).getByLabelText('Nome da peça'), 'Filtro de óleo')
    await typist.type(within(filter).getByLabelText('Part number (opcional)'), 'W 610/1')
    await typist.type(within(filter).getByLabelText('Preço unitário (opcional)'), '1.038,00')

    await typist.click(screen.getByRole('button', { name: 'Salvar registro' }))

    // Depois de salvar, vai para o detalhe do registro (onde ficam os anexos).
    expect(await screen.findByRole('heading', { level: 1, name: 'Troca de óleo e filtro' })).toBeInTheDocument()
    const [create] = sent(calls, 'POST', recordsPath)
    expect(create.headers.get('X-XSRF-TOKEN')).toBe('test-token')
    expect(bodyOf(create)).toEqual({
      type: 'maintenance',
      date: '2026-10-12',
      odometer: 214500,
      title: 'Troca de óleo e filtro',
      description: null,
      category: 'engine',
      performedBy: null,
      laborCost: 150,
      replacedOriginalPart: null,
      parts: [
        { name: 'Óleo 5W30', brand: 'Motul', partNumber: null, quantity: 3.5, unit: 'liter', unitPrice: 45.9 },
        { name: 'Filtro de óleo', brand: null, partNumber: 'W 610/1', quantity: 1, unit: 'unit', unitPrice: 1038 },
      ],
      fulfilledReminderIds: [],
    })
  })

  it('km fora da ordem do histórico (422): mostra a faixa válida no campo da km', async () => {
    fakeApi({
      [`POST ${recordsPath}`]: () =>
        problem(422, {
          code: 'record.odometer_out_of_order',
          detail: 'A km deve ficar entre 80.000 e 90.000 km para manter a ordem do histórico.',
          maxKmBefore: 80000,
          minKmAfter: 90000,
        }),
    })
    renderApp(`/vehicles/${id}/records/new`)
    const typist = userEvent.setup()

    await typist.type(await screen.findByLabelText('Título'), 'Troca de pastilhas')
    await typist.selectOptions(screen.getByLabelText('Categoria'), 'brakes')
    await typist.type(screen.getByLabelText('Km (opcional)'), '95000')
    await typist.click(screen.getByRole('button', { name: 'Salvar registro' }))

    expect(await screen.findByText('Entre 80.000 e 90.000 km')).toBeInTheDocument()
    expect(screen.getByLabelText('Km (opcional)')).toHaveAccessibleDescription(/Entre 80\.000 e 90\.000 km/)
    // Continua no formulário, com o que foi digitado.
    expect(screen.getByLabelText('Título')).toHaveValue('Troca de pastilhas')
  })

  it('"peça original substituída" só existe para modificação e não vai na manutenção', async () => {
    const calls = fakeApi({ [`POST ${recordsPath}`]: () => json({ id: recordId }, 201) })
    renderApp(`/vehicles/${id}/records/new`)
    const typist = userEvent.setup()

    await screen.findByLabelText('Título')
    expect(screen.getByRole('radio', { name: 'Manutenção' })).toBeChecked()
    expect(screen.queryByLabelText('Peça original substituída (opcional)')).not.toBeInTheDocument()

    await typist.click(screen.getByRole('radio', { name: 'Modificação' }))
    await typist.type(screen.getByLabelText('Peça original substituída (opcional)'), 'Amortecedores originais guardados')

    await typist.click(screen.getByRole('radio', { name: 'Manutenção' }))
    expect(screen.queryByLabelText('Peça original substituída (opcional)')).not.toBeInTheDocument()

    await typist.type(screen.getByLabelText('Título'), 'Alinhamento')
    await typist.selectOptions(screen.getByLabelText('Categoria'), 'suspension')
    await typist.click(screen.getByRole('button', { name: 'Salvar registro' }))

    await screen.findByRole('heading', { level: 1, name: 'Troca de óleo e filtro' })
    // O texto digitado antes de voltar para manutenção não segue (o back-end recusaria com 422).
    expect(bodyOf(sent(calls, 'POST', recordsPath)[0])).toMatchObject({ type: 'maintenance', replacedOriginalPart: null })
  })

  it('modificação manda a peça original substituída', async () => {
    const calls = fakeApi({ [`POST ${recordsPath}`]: () => json({ id: recordId }, 201) })
    renderApp(`/vehicles/${id}/records/new`)
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('radio', { name: 'Modificação' }))
    await typist.type(screen.getByLabelText('Título'), 'Coilover')
    await typist.selectOptions(screen.getByLabelText('Categoria'), 'suspension')
    await typist.type(screen.getByLabelText('Peça original substituída (opcional)'), 'Amortecedores originais guardados')
    await typist.click(screen.getByRole('button', { name: 'Salvar registro' }))

    await screen.findByRole('heading', { level: 1, name: 'Troca de óleo e filtro' })
    expect(bodyOf(sent(calls, 'POST', recordsPath)[0])).toMatchObject({
      type: 'modification',
      replacedOriginalPart: 'Amortecedores originais guardados',
    })
  })

  it('"cumpre lembretes": marca os lembretes do veículo e manda os ids', async () => {
    const calls = fakeApi({
      [`GET /api/vehicles/${id}/reminders`]: () => json([oilReminder, beltReminder]),
      [`POST ${recordsPath}`]: () => json({ id: recordId }, 201),
    })
    renderApp(`/vehicles/${id}/records/new`)
    const typist = userEvent.setup()

    await typist.type(await screen.findByLabelText('Título'), 'Troca de óleo')
    await typist.selectOptions(screen.getByLabelText('Categoria'), 'engine')
    await typist.click(await screen.findByRole('checkbox', { name: /Troca de óleo/ }))
    expect(screen.getByRole('checkbox', { name: /Correia dentada/ })).not.toBeChecked()
    await typist.click(screen.getByRole('button', { name: 'Salvar registro' }))

    await screen.findByRole('heading', { level: 1, name: 'Troca de óleo e filtro' })
    expect(bodyOf(sent(calls, 'POST', recordsPath)[0])).toMatchObject({ fulfilledReminderIds: [oilReminder.id] })
  })

  it('campos inválidos são recusados antes de chamar a API', async () => {
    const calls = fakeApi()
    renderApp(`/vehicles/${id}/records/new`)
    const typist = userEvent.setup()

    await typist.type(await screen.findByLabelText('Km (opcional)'), '12 mil')
    await typist.type(screen.getByLabelText('Mão de obra (opcional)'), '150.50')
    const part = await addPart(typist, 1)
    await typist.clear(within(part).getByLabelText('Quantidade'))
    await typist.type(within(part).getByLabelText('Quantidade'), '0')
    await typist.click(screen.getByRole('button', { name: 'Salvar registro' }))

    expect(await screen.findByText('Informe o título.')).toBeInTheDocument()
    expect(screen.getByText('Escolha a categoria.')).toBeInTheDocument()
    expect(screen.getByText('Informe a km só com números, de 0 a 2.000.000.')).toBeInTheDocument()
    expect(screen.getByText(/Informe o valor em reais/)).toBeInTheDocument()
    expect(within(part).getByText('Informe o nome da peça.')).toBeInTheDocument()
    expect(within(part).getByText(/A quantidade deve ser maior que zero/)).toBeInTheDocument()
    expect(sent(calls, 'POST', recordsPath)).toHaveLength(0)
  })

  it('data no futuro é recusada sem chamar a API', async () => {
    const calls = fakeApi()
    renderApp(`/vehicles/${id}/records/new`)
    const typist = userEvent.setup()

    const date = await screen.findByLabelText('Data')
    expect(date).toHaveValue('2026-10-12')
    await typist.clear(date)
    await typist.type(date, '2026-10-13')
    await typist.type(screen.getByLabelText('Título'), 'Revisão')
    await typist.selectOptions(screen.getByLabelText('Categoria'), 'other')
    await typist.click(screen.getByRole('button', { name: 'Salvar registro' }))

    expect(await screen.findByText('A data não pode estar no futuro.')).toBeInTheDocument()
    expect(sent(calls, 'POST', recordsPath)).toHaveLength(0)
  })

  it('erro de validação da API vai para o campo (peças com índice), e o que não é de campo vai para o aviso', async () => {
    fakeApi({
      [`POST ${recordsPath}`]: () =>
        problem(400, {
          code: 'validation.failed',
          detail: 'Um ou mais campos são inválidos.',
          errors: {
            title: ['O título deve ter no máximo 120 caracteres.'],
            'parts[0].unitPrice': ['O preço unitário deve estar entre R$ 0,00 e R$ 9.999.999.999,99, com no máximo 2 casas decimais.'],
            totalCost: ['O custo total do registro (peças + mão de obra) passa do limite de R$ 9.999.999.999,99.'],
          },
        }),
    })
    renderApp(`/vehicles/${id}/records/new`)
    const typist = userEvent.setup()

    await typist.type(await screen.findByLabelText('Título'), 'Algo')
    await typist.selectOptions(screen.getByLabelText('Categoria'), 'other')
    const part = await addPart(typist, 1)
    await typist.type(within(part).getByLabelText('Nome da peça'), 'Peça')
    await typist.click(screen.getByRole('button', { name: 'Salvar registro' }))

    expect(await screen.findByText('O título deve ter no máximo 120 caracteres.')).toBeInTheDocument()
    expect(within(part).getByText(/O preço unitário deve estar entre/)).toBeInTheDocument()
    // "totalCost" não é campo do formulário: aparece no aviso geral, não se perde.
    expect(screen.getByText(/O custo total do registro/)).toBeInTheDocument()
  })

  it('o botão fica desabilitado enquanto salva (sem registro duplicado)', async () => {
    let release: () => void = () => undefined
    const calls = fakeApi({
      [`POST ${recordsPath}`]: () =>
        new Promise<Response>((resolve) => {
          release = () => resolve(json({ id: recordId }, 201))
        }),
    })
    renderApp(`/vehicles/${id}/records/new`)
    const typist = userEvent.setup()

    await typist.type(await screen.findByLabelText('Título'), 'Revisão')
    await typist.selectOptions(screen.getByLabelText('Categoria'), 'other')
    const save = screen.getByRole('button', { name: 'Salvar registro' })
    await typist.click(save)
    await waitFor(() => expect(save).toBeDisabled())
    await typist.click(save)
    expect(sent(calls, 'POST', recordsPath)).toHaveLength(1)

    release()
    await screen.findByRole('heading', { level: 1, name: 'Troca de óleo e filtro' })
  })
})

describe('editar registro', () => {
  const stored = {
    ...savedRecord,
    type: 'modification',
    title: 'Coilover Tein',
    category: 'suspension',
    description: 'Regulagem inicial na altura mais baixa.',
    performedBy: 'Oficina do Zé',
    replacedOriginalPart: 'Amortecedores originais guardados',
    laborCost: 300,
    parts: [
      { name: 'Coilover Tein Street', brand: 'Tein', partNumber: 'GSE90', quantity: 1, unit: 'unit', unitPrice: 4250, lineTotal: 4250 },
    ],
    totalCost: 4550,
    rowVersion: 5,
  }

  it('abre com os dados do registro, sem "cumpre lembretes", e salva com rowVersion', async () => {
    const calls = fakeApi({
      [`GET ${recordsPath}/${recordId}`]: () => json(stored),
      [`GET /api/vehicles/${id}/reminders`]: () => json([oilReminder]),
      [`PUT ${recordsPath}/${recordId}`]: () => noContent(),
    })
    renderApp(`/vehicles/${id}/records/${recordId}/edit`)
    const typist = userEvent.setup()

    const title = await screen.findByLabelText('Título')
    expect(title).toHaveValue('Coilover Tein')
    expect(screen.getByRole('radio', { name: 'Modificação' })).toBeChecked()
    expect(screen.getByLabelText('Categoria')).toHaveValue('suspension')
    expect(screen.getByLabelText('Peça original substituída (opcional)')).toHaveValue('Amortecedores originais guardados')
    expect(screen.getByLabelText('Mão de obra (opcional)')).toHaveValue('300,00')
    const part = screen.getByRole('group', { name: 'Peça 1' })
    expect(within(part).getByLabelText('Preço unitário (opcional)')).toHaveValue('4.250,00')
    expect(screen.queryByRole('checkbox', { name: /Troca de óleo/ })).not.toBeInTheDocument()

    await typist.clear(title)
    await typist.type(title, 'Coilover Tein Street')
    await typist.click(screen.getByRole('button', { name: 'Salvar alterações' }))

    await waitFor(() => expect(sent(calls, 'PUT', `${recordsPath}/${recordId}`)).toHaveLength(1))
    const body = bodyOf(sent(calls, 'PUT', `${recordsPath}/${recordId}`)[0])
    expect(body).toMatchObject({
      type: 'modification',
      title: 'Coilover Tein Street',
      odometer: 214500,
      laborCost: 300,
      rowVersion: 5,
      parts: [{ name: 'Coilover Tein Street', brand: 'Tein', partNumber: 'GSE90', quantity: 1, unit: 'unit', unitPrice: 4250 }],
    })
    expect(body).not.toHaveProperty('fulfilledReminderIds')
  })

  it('409 (alterado em outra tela): avisa, busca o registro de novo e recomeça com o que há no servidor', async () => {
    let title = 'Coilover Tein'
    let rowVersion = 5
    fakeApi({
      [`GET ${recordsPath}/${recordId}`]: () => json({ ...stored, title, rowVersion }),
      [`PUT ${recordsPath}/${recordId}`]: () => {
        title = 'Coilover Tein (editado em outra aba)'
        rowVersion = 6
        return problem(409, { code: 'concurrency.conflict', detail: 'O registro foi alterado por outra operação.' })
      },
    })
    renderApp(`/vehicles/${id}/records/${recordId}/edit`)
    const typist = userEvent.setup()

    await screen.findByLabelText('Título')
    await typist.click(screen.getByRole('button', { name: 'Salvar alterações' }))

    expect(await screen.findByText(/Este item foi alterado em outra tela/)).toBeInTheDocument()
    await waitFor(() => expect(screen.getByLabelText('Título')).toHaveValue('Coilover Tein (editado em outra aba)'))
  })
})
