import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { json, mockFetch, noContent, problem, resetCookies, type Call, type Handler } from '../test/fetch-mock'
import { renderApp } from '../test/renderApp'
import { signedInRoutes } from '../test/signedIn'

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
  notes: 'Midnight Purple',
  currentOdometer: 215000,
  // 01:30 UTC de 12/10 ainda é 11/10 em Brasília.
  odometerUpdatedAt: '2026-10-12T01:30:00+00:00',
  catalog: { type: 'car', fipeCode: '005340-6', fipeYearCode: '1998-1' },
  rowVersion: 7,
}

const makes = [{ id: '25', name: 'Honda' }]
const models = [
  { id: '4828', fullName: 'Civic Sedan LX 1.6 16V Mec. 4p', suggestedModel: 'Civic', suggestedVersion: 'Sedan LX 1.6 16V Mec. 4p' },
]
const years = [{ id: '1998-1', modelYear: 1998, fuel: 'gasoline', label: '1998 Gasolina' }]
const details = {
  fipeCode: '005340-6',
  make: 'Honda',
  fullName: 'Civic Sedan LX 1.6 16V Mec. 4p',
  modelYear: 1998,
  fuel: 'gasoline',
  price: 23456.78,
  referenceMonth: 'outubro de 2026',
}

const catalogRoutes: Record<string, Handler> = {
  'GET /api/catalog/car/makes': () => json(makes),
  'GET /api/catalog/car/makes/25/models': () => json(models),
  'GET /api/catalog/car/makes/25/models/4828/years': () => json(years),
  'GET /api/catalog/car/makes/25/models/4828/years/1998-1': () => json(details),
}

function fakeApi(routes: Record<string, Handler> = {}) {
  return mockFetch({
    ...signedInRoutes(),
    'GET /api/vehicles': () => json([]),
    'GET /api/reminders/due': () => json([]),
    ...routes,
  })
}

const sent = (calls: Call[], method: string, path: string) =>
  calls.filter((call) => call.method === method && call.path === path)

const bodyOf = (call: Call | undefined) => JSON.parse(call?.body ?? 'null') as unknown

beforeEach(() => {
  resetCookies()
})

describe('novo veículo', () => {
  it('escolhe na FIPE, confere o que veio preenchido, informa a km e cadastra com o vínculo FIPE', async () => {
    const calls = fakeApi({
      ...catalogRoutes,
      'POST /api/vehicles': () => json({ id }, 201),
      [`GET /api/vehicles/${id}`]: () => json(civicDto),
    })
    renderApp('/vehicles/new')
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Carro' }))
    await typist.click(await screen.findByRole('button', { name: 'Honda' }))
    await typist.click(await screen.findByRole('button', { name: 'Civic Sedan LX 1.6 16V Mec. 4p' }))
    await typist.click(await screen.findByRole('button', { name: '1998 Gasolina' }))

    // O formulário abre preenchido (tudo editável), com o tipo travado pelo vínculo.
    expect(await screen.findByLabelText('Marca')).toHaveValue('Honda')
    expect(screen.getByLabelText('Modelo')).toHaveValue('Civic')
    expect(screen.getByLabelText('Versão (opcional)')).toHaveValue('Sedan LX 1.6 16V Mec. 4p')
    expect(screen.getByLabelText('Ano de fabricação')).toHaveValue('1998')
    expect(screen.getByLabelText('Ano-modelo')).toHaveValue('1998')
    expect(screen.getByLabelText('Combustível')).toHaveValue('gasoline')
    expect(screen.getByLabelText('Tipo')).toBeDisabled()
    expect(screen.getByText('Dados da FIPE')).toBeInTheDocument()

    await typist.type(screen.getByLabelText('Geração (opcional)'), 'EK')
    await typist.type(screen.getByLabelText('Apelido (opcional)'), 'Civic EK')
    await typist.type(screen.getByLabelText('Placa (opcional)'), 'abc-1d23')
    await typist.type(screen.getByLabelText('Km atual'), '215.000')
    await typist.click(screen.getByRole('button', { name: 'Cadastrar veículo' }))

    // Vai para a tela do veículo recém-criado.
    expect(await screen.findByRole('heading', { level: 1, name: 'Civic EK' })).toBeInTheDocument()
    const [create] = sent(calls, 'POST', '/api/vehicles')
    expect(create.headers.get('X-XSRF-TOKEN')).toBe('test-token')
    expect(bodyOf(create)).toEqual({
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
      catalog: { type: 'car', fipeCode: '005340-6', fipeYearCode: '1998-1' },
      currentOdometer: 215000,
    })
  })

  it('FIPE fora do ar (503): abre o formulário manual com o aviso, e cadastrar não depende dela', async () => {
    const calls = fakeApi({
      'GET /api/catalog/car/makes': () =>
        problem(503, { code: 'catalog.unavailable', detail: 'Catálogo FIPE indisponível no momento. Preencha manualmente.' }),
      'POST /api/vehicles': () => json({ id }, 201),
      [`GET /api/vehicles/${id}`]: () => json(civicDto),
    })
    renderApp('/vehicles/new')
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Carro' }))

    expect(await screen.findByText(/A tabela FIPE não respondeu agora/)).toBeInTheDocument()
    expect(screen.getByLabelText('Tipo')).toBeEnabled()

    await typist.type(screen.getByLabelText('Marca'), 'Honda')
    await typist.type(screen.getByLabelText('Modelo'), 'Civic')
    await typist.type(screen.getByLabelText('Ano de fabricação'), '1998')
    await typist.selectOptions(screen.getByLabelText('Combustível'), 'gasoline')
    await typist.type(screen.getByLabelText('Km atual'), '215000')
    await typist.click(screen.getByRole('button', { name: 'Cadastrar veículo' }))

    await screen.findByRole('heading', { level: 1, name: 'Civic EK' })
    // Sem FIPE: catalog nulo; o ano-modelo acompanhou o de fabricação.
    expect(bodyOf(sent(calls, 'POST', '/api/vehicles')[0])).toMatchObject({
      make: 'Honda',
      model: 'Civic',
      manufactureYear: 1998,
      modelYear: 1998,
      fuel: 'gasoline',
      plate: null,
      catalog: null,
      currentOdometer: 215000,
    })
  })

  it('"Não achei, preencher manualmente" abre o formulário, que valida antes de chamar a API', async () => {
    const calls = fakeApi()
    renderApp('/vehicles/new')
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Não achei, preencher manualmente' }))
    expect(screen.queryByText(/A tabela FIPE não respondeu/)).not.toBeInTheDocument()
    await typist.click(screen.getByRole('button', { name: 'Cadastrar veículo' }))

    expect(await screen.findByText('Informe a marca.')).toBeInTheDocument()
    expect(screen.getByText('Informe o modelo.')).toBeInTheDocument()
    expect(screen.getByText('Escolha o combustível.')).toBeInTheDocument()
    expect(screen.getByText('Informe a km só com números, de 0 a 2.000.000.')).toBeInTheDocument()
    expect(sent(calls, 'POST', '/api/vehicles')).toHaveLength(0)
  })

  it('o ano-modelo acompanha o de fabricação só até o usuário mexer nele', async () => {
    fakeApi()
    renderApp('/vehicles/new')
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Não achei, preencher manualmente' }))
    await typist.type(screen.getByLabelText('Ano de fabricação'), '1998')
    expect(screen.getByLabelText('Ano-modelo')).toHaveValue('1998')

    await typist.clear(screen.getByLabelText('Ano-modelo'))
    await typist.type(screen.getByLabelText('Ano-modelo'), '1999')
    await typist.clear(screen.getByLabelText('Ano de fabricação'))
    await typist.type(screen.getByLabelText('Ano de fabricação'), '1997')
    expect(screen.getByLabelText('Ano-modelo')).toHaveValue('1999')
  })

  it('ano-modelo fora da regra (fabricação ou o seguinte) é recusado sem chamar a API', async () => {
    const calls = fakeApi()
    renderApp('/vehicles/new')
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Não achei, preencher manualmente' }))
    await typist.type(screen.getByLabelText('Ano de fabricação'), '1998')
    await typist.clear(screen.getByLabelText('Ano-modelo'))
    await typist.type(screen.getByLabelText('Ano-modelo'), '2001')
    await typist.click(screen.getByRole('button', { name: 'Cadastrar veículo' }))

    expect(await screen.findByText('O ano-modelo é o ano de fabricação ou o seguinte.')).toBeInTheDocument()
    expect(sent(calls, 'POST', '/api/vehicles')).toHaveLength(0)
  })

  it('erro de validação da API vai para o campo; o que não é de campo vai para o aviso', async () => {
    fakeApi({
      'POST /api/vehicles': () =>
        problem(400, {
          code: 'validation.failed',
          detail: 'Um ou mais erros de validação.',
          errors: { plate: ['Placa inválida.'], 'catalog.fipeCode': ['Informe o código FIPE.'] },
        }),
    })
    renderApp('/vehicles/new')
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Não achei, preencher manualmente' }))
    await typist.type(screen.getByLabelText('Marca'), 'Honda')
    await typist.type(screen.getByLabelText('Modelo'), 'Civic')
    await typist.type(screen.getByLabelText('Ano de fabricação'), '1998')
    await typist.selectOptions(screen.getByLabelText('Combustível'), 'gasoline')
    await typist.type(screen.getByLabelText('Km atual'), '215000')
    await typist.click(screen.getByRole('button', { name: 'Cadastrar veículo' }))

    expect(await screen.findByText('Informe o código FIPE.')).toBeInTheDocument()
    expect(screen.getByLabelText('Placa (opcional)')).toHaveAccessibleDescription(/Placa inválida\./)
    expect(screen.queryByText('Um ou mais erros de validação.')).not.toBeInTheDocument()
  })

  it('o botão fica desabilitado enquanto cadastra (sem veículo duplicado)', async () => {
    let release: () => void = () => undefined
    const calls = fakeApi({
      'POST /api/vehicles': () =>
        new Promise<Response>((resolve) => {
          release = () => resolve(json({ id }, 201))
        }),
      [`GET /api/vehicles/${id}`]: () => json(civicDto),
    })
    renderApp('/vehicles/new')
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Não achei, preencher manualmente' }))
    await typist.type(screen.getByLabelText('Marca'), 'Honda')
    await typist.type(screen.getByLabelText('Modelo'), 'Civic')
    await typist.type(screen.getByLabelText('Ano de fabricação'), '1998')
    await typist.selectOptions(screen.getByLabelText('Combustível'), 'gasoline')
    await typist.type(screen.getByLabelText('Km atual'), '215000')
    const submit = screen.getByRole('button', { name: 'Cadastrar veículo' })
    await typist.click(submit)

    await waitFor(() => expect(submit).toBeDisabled())
    await typist.click(submit)
    expect(sent(calls, 'POST', '/api/vehicles')).toHaveLength(1)

    release()
    await screen.findByRole('heading', { level: 1, name: 'Civic EK' })
  })
})

describe('editar veículo', () => {
  const editRoutes = (extra: Record<string, Handler> = {}) => ({
    [`GET /api/vehicles/${id}`]: () => json(civicDto),
    ...extra,
  })

  it('abre com os dados atuais, sem o passo da FIPE nem a km, e salva mandando rowVersion e o vínculo', async () => {
    const calls = fakeApi(editRoutes({ [`PUT /api/vehicles/${id}`]: () => noContent() }))
    renderApp(`/vehicles/${id}/edit`)
    const typist = userEvent.setup()

    expect(await screen.findByLabelText('Marca')).toHaveValue('Honda')
    expect(screen.getByLabelText('Apelido (opcional)')).toHaveValue('Civic EK')
    expect(screen.getByLabelText('Tipo')).toBeDisabled()
    expect(screen.queryByLabelText('Km atual')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Carro' })).not.toBeInTheDocument()

    await typist.clear(screen.getByLabelText('Apelido (opcional)'))
    await typist.type(screen.getByLabelText('Apelido (opcional)'), 'Midnight Purple')
    await typist.click(screen.getByRole('button', { name: 'Salvar alterações' }))

    await screen.findByRole('heading', { level: 1, name: 'Civic EK' }) // volta para a tela do veículo
    const [update] = sent(calls, 'PUT', `/api/vehicles/${id}`)
    expect(update.headers.get('X-XSRF-TOKEN')).toBe('test-token')
    expect(bodyOf(update)).toEqual({
      type: 'car',
      make: 'Honda',
      model: 'Civic',
      version: 'Sedan LX 1.6 16V Mec. 4p',
      generation: 'EK',
      manufactureYear: 1998,
      modelYear: 1998,
      fuel: 'gasoline',
      nickname: 'Midnight Purple',
      plate: 'ABC1D23',
      vin: null,
      notes: 'Midnight Purple',
      catalog: { type: 'car', fipeCode: '005340-6', fipeYearCode: '1998-1' },
      rowVersion: 7,
    })
  })

  it('409 (alterado em outra tela): avisa, busca o veículo de novo e recomeça com o que há no servidor', async () => {
    let saved = false
    const calls = fakeApi(
      editRoutes({
        [`GET /api/vehicles/${id}`]: () => json(saved ? { ...civicDto, nickname: 'Apelido da outra aba', rowVersion: 8 } : civicDto),
        [`PUT /api/vehicles/${id}`]: () => {
          saved = true
          return problem(409, { code: 'concurrency.conflict', detail: 'O registro foi alterado por outra operação.' })
        },
      }),
    )
    renderApp(`/vehicles/${id}/edit`)
    const typist = userEvent.setup()

    await typist.clear(await screen.findByLabelText('Apelido (opcional)'))
    await typist.type(screen.getByLabelText('Apelido (opcional)'), 'Minha edição')
    await typist.click(screen.getByRole('button', { name: 'Salvar alterações' }))

    expect(await screen.findByText(/Este item foi alterado em outra tela/)).toBeInTheDocument()
    await waitFor(() => expect(screen.getByLabelText('Apelido (opcional)')).toHaveValue('Apelido da outra aba'))
    expect(sent(calls, 'GET', `/api/vehicles/${id}`)).toHaveLength(2)
    expect(sent(calls, 'PUT', `/api/vehicles/${id}`)).toHaveLength(1)
    // Salvar de novo usa a versão nova.
    saved = false
    await typist.click(screen.getByRole('button', { name: 'Salvar alterações' }))
    await waitFor(() => expect(sent(calls, 'PUT', `/api/vehicles/${id}`)).toHaveLength(2))
    expect(bodyOf(sent(calls, 'PUT', `/api/vehicles/${id}`)[1])).toMatchObject({ rowVersion: 8 })
  })

  it('erro de validação da API vai para o campo', async () => {
    fakeApi(
      editRoutes({
        [`PUT /api/vehicles/${id}`]: () =>
          problem(400, { code: 'validation.failed', errors: { modelYear: ['Ano inválido.'] } }),
      }),
    )
    renderApp(`/vehicles/${id}/edit`)

    await screen.findByLabelText('Marca')
    await userEvent.click(screen.getByRole('button', { name: 'Salvar alterações' }))

    expect(await screen.findByText('Ano inválido.')).toBeInTheDocument()
    expect(screen.getByLabelText('Ano-modelo')).toHaveAccessibleDescription('Ano inválido.')
  })

  it('veículo que não existe (ou não é seu) mostra "Veículo não encontrado"', async () => {
    fakeApi({ [`GET /api/vehicles/${id}`]: () => problem(404, { code: 'vehicle.not_found', detail: 'Veículo não encontrado.' }) })
    renderApp(`/vehicles/${id}/edit`)

    expect(await screen.findByRole('heading', { name: 'Veículo não encontrado' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Voltar ao painel' })).toHaveAttribute('href', '/')
  })

  it('excluir pede confirmação, apaga e volta ao painel', async () => {
    let deleted = false
    const calls = fakeApi(
      editRoutes({
        'GET /api/vehicles': () => json(deleted ? [] : [{ id, displayName: 'Civic EK', make: 'Honda', model: 'Civic', modelYear: 1998, plate: 'ABC1D23', currentOdometer: 215000 }]),
        [`DELETE /api/vehicles/${id}`]: () => {
          deleted = true
          return noContent()
        },
      }),
    )
    renderApp(`/vehicles/${id}/edit`)
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Excluir veículo' }))
    expect(sent(calls, 'DELETE', `/api/vehicles/${id}`)).toHaveLength(0) // só abriu a confirmação
    const dialog = screen.getByRole('dialog', { name: 'Excluir este veículo?' })
    expect(dialog).toHaveTextContent('Civic EK')

    await typist.click(screen.getByRole('button', { name: 'Excluir de vez' }))

    expect(await screen.findByText('Sua garagem está vazia')).toBeInTheDocument()
    expect(sent(calls, 'DELETE', `/api/vehicles/${id}`)).toHaveLength(1)
    // O veículo apagado não é buscado de novo (daria 404).
    expect(sent(calls, 'GET', `/api/vehicles/${id}`)).toHaveLength(1)
  })

  it('"Cancelar" na confirmação não apaga nada', async () => {
    const calls = fakeApi(editRoutes())
    renderApp(`/vehicles/${id}/edit`)
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Excluir veículo' }))
    await typist.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancelar' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(sent(calls, 'DELETE', `/api/vehicles/${id}`)).toHaveLength(0)
  })
})
