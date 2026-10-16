import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { json, mockFetch, noContent, problem, resetCookies, type Call, type Handler } from '../../test/fetch-mock'
import { renderApp } from '../../test/renderApp'
import { signedInRoutes, vehicleSummaryRoute } from '../../test/signedIn'

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
  currentOdometer: 215000,
  odometerUpdatedAt: '2026-10-12T01:30:00+00:00', // ainda 11/10 em Brasília
  catalog: null,
  rowVersion: 7,
}

const summary = { id, displayName: 'Civic EK', make: 'Honda', model: 'Civic', modelYear: 1998, plate: 'ABC1D23', currentOdometer: 215000 }

function fakeApi(routes: Record<string, Handler> = {}) {
  return mockFetch({
    ...signedInRoutes(),
    ...vehicleSummaryRoute(id),
    'GET /api/vehicles': () => json([summary]),
    'GET /api/reminders/due': () => json([]),
    [`GET /api/vehicles/${id}`]: () => json(civicDto),
    // A tela do veículo também abre o histórico (aqui vazio).
    [`GET /api/vehicles/${id}/records`]: () => json({ items: [], nextCursor: null }),
    ...routes,
  })
}

const sent = (calls: Call[], method: string, path: string) =>
  calls.filter((call) => call.method === method && call.path === path)

const bodyOf = (call: Call | undefined) => JSON.parse(call?.body ?? 'null') as unknown

async function openDialog() {
  const typist = userEvent.setup()
  await typist.click(await screen.findByRole('button', { name: 'Atualizar km' }))
  return { typist, dialog: await screen.findByRole('dialog') }
}

beforeEach(() => {
  resetCookies()
})

describe('tela do veículo e atualização de km', () => {
  it('mostra o cabeçalho com nome, placa, km formatada e a data da última atualização em Brasília', async () => {
    fakeApi()
    renderApp(`/vehicles/${id}`)

    expect(await screen.findByRole('heading', { level: 1, name: 'Civic EK' })).toBeInTheDocument()
    expect(screen.getByText('ABC1D23')).toBeInTheDocument()
    expect(screen.getByText('215.000 km')).toBeInTheDocument()
    expect(screen.getByText('atualizada em 11/10/2026')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Editar' })).toHaveAttribute('href', `/vehicles/${id}/edit`)
  })

  it('atualiza a km: manda o número, fecha a janela e a tela mostra a km nova', async () => {
    let km = 215000
    const calls = fakeApi({
      [`GET /api/vehicles/${id}`]: () => json({ ...civicDto, currentOdometer: km }),
      [`POST /api/vehicles/${id}/odometer`]: () => {
        km = 216500
        return noContent()
      },
    })
    renderApp(`/vehicles/${id}`)
    const { typist, dialog } = await openDialog()

    const field = within(dialog).getByLabelText('Km do painel')
    expect(field).toHaveValue('215000')
    await typist.clear(field)
    await typist.type(field, '216.500')
    await typist.click(within(dialog).getByRole('button', { name: 'Salvar km' }))

    expect(await screen.findByText('216.500 km')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    const [update] = sent(calls, 'POST', `/api/vehicles/${id}/odometer`)
    expect(update.headers.get('X-XSRF-TOKEN')).toBe('test-token')
    expect(bodyOf(update)).toEqual({ km: 216500 })
  })

  it('a km nova deixa os lembretes e a lista de veículos velhos (o painel busca de novo)', async () => {
    const calls = fakeApi({ [`POST /api/vehicles/${id}/odometer`]: () => noContent() })
    const { queryClient } = renderApp('/')
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('heading', { level: 3, name: 'Civic EK' }))
    await typist.click(await screen.findByRole('button', { name: 'Atualizar km' }))

    await typist.clear(screen.getByLabelText('Km do painel'))
    await typist.type(screen.getByLabelText('Km do painel'), '216000')
    // Os lembretes vencidos e próximos (painel e contador do menu) estão na tela: a km nova os busca de novo.
    const dueBefore = calls.filter((call) => call.path === '/api/reminders/due').length
    await typist.click(screen.getByRole('button', { name: 'Salvar km' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    await waitFor(() => expect(calls.filter((call) => call.path === '/api/reminders/due').length).toBeGreaterThan(dueBefore))
    expect(queryClient.getQueryState(['vehicles'])?.isInvalidated).toBe(true)
  })

  it('km menor (422): mostra a mensagem da API e oferece corrigir; a correção manda para o endereço certo', async () => {
    const calls = fakeApi({
      [`POST /api/vehicles/${id}/odometer`]: () =>
        problem(422, {
          code: 'vehicle.odometer_cannot_decrease',
          detail: 'A quilometragem não pode diminuir. Se digitou errado, use a correção de km.',
        }),
      [`POST /api/vehicles/${id}/odometer/correction`]: () => noContent(),
    })
    renderApp(`/vehicles/${id}`)
    const { typist, dialog } = await openDialog()

    await typist.clear(within(dialog).getByLabelText('Km do painel'))
    await typist.type(within(dialog).getByLabelText('Km do painel'), '210000')
    await typist.click(within(dialog).getByRole('button', { name: 'Salvar km' }))

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('A quilometragem não pode diminuir.')
    expect(screen.getByRole('dialog')).toBeInTheDocument() // continua aberta

    await typist.click(within(dialog).getByRole('button', { name: 'Digitei a km errada antes? Corrigir km' }))
    expect(within(dialog).queryByRole('alert')).not.toBeInTheDocument() // mudar de modo limpa o aviso
    expect(within(dialog).getByRole('heading', { name: 'Corrigir km' })).toBeInTheDocument()
    await typist.click(within(dialog).getByRole('button', { name: 'Corrigir km' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(bodyOf(sent(calls, 'POST', `/api/vehicles/${id}/odometer/correction`)[0])).toEqual({ km: 210000 })
    expect(sent(calls, 'POST', `/api/vehicles/${id}/odometer`)).toHaveLength(1)
  })

  it('correção abaixo da maior km do histórico (422) mostra a mensagem da API', async () => {
    fakeApi({
      [`POST /api/vehicles/${id}/odometer/correction`]: () =>
        problem(422, {
          code: 'vehicle.odometer_below_records',
          detail: 'A km corrigida não pode ficar abaixo de 212.000 km, a maior km do histórico.',
        }),
    })
    renderApp(`/vehicles/${id}`)
    const { typist, dialog } = await openDialog()

    await typist.click(within(dialog).getByRole('button', { name: 'Digitei a km errada antes? Corrigir km' }))
    await typist.clear(within(dialog).getByLabelText('Km correta'))
    await typist.type(within(dialog).getByLabelText('Km correta'), '100000')
    await typist.click(within(dialog).getByRole('button', { name: 'Corrigir km' }))

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('abaixo de 212.000 km')
  })

  it('valor que não é km é recusado antes de chamar a API', async () => {
    const calls = fakeApi()
    renderApp(`/vehicles/${id}`)
    const { typist, dialog } = await openDialog()

    await typist.clear(within(dialog).getByLabelText('Km do painel'))
    await typist.type(within(dialog).getByLabelText('Km do painel'), '12 mil')
    await typist.click(within(dialog).getByRole('button', { name: 'Salvar km' }))

    expect(await within(dialog).findByText('Informe a km só com números, de 0 a 2.000.000.')).toBeInTheDocument()
    expect(sent(calls, 'POST', `/api/vehicles/${id}/odometer`)).toHaveLength(0)
  })

  it('409 (alterado em outra tela): avisa e busca o veículo de novo', async () => {
    let km = 215000
    const calls = fakeApi({
      [`GET /api/vehicles/${id}`]: () => json({ ...civicDto, currentOdometer: km }),
      [`POST /api/vehicles/${id}/odometer`]: () => {
        km = 230000 // outra aba atualizou antes
        return problem(409, { code: 'concurrency.conflict', detail: 'O registro foi alterado por outra operação.' })
      },
    })
    renderApp(`/vehicles/${id}`)
    const { typist, dialog } = await openDialog()

    await typist.clear(within(dialog).getByLabelText('Km do painel'))
    await typist.type(within(dialog).getByLabelText('Km do painel'), '216000')
    await typist.click(within(dialog).getByRole('button', { name: 'Salvar km' }))

    expect(await within(dialog).findByText(/Este item foi alterado em outra tela/)).toBeInTheDocument()
    // A km nova aparece na tela e também na janela ("Km registrada hoje"), que segue aberta para tentar de novo.
    await waitFor(() => expect(screen.getAllByText('230.000 km')).toHaveLength(2))
    expect(sent(calls, 'GET', `/api/vehicles/${id}`)).toHaveLength(2)
  })

  it('o botão fica desabilitado enquanto salva (sem envio em dobro)', async () => {
    let release: () => void = () => undefined
    const calls = fakeApi({
      [`POST /api/vehicles/${id}/odometer`]: () =>
        new Promise<Response>((resolve) => {
          release = () => resolve(noContent())
        }),
    })
    renderApp(`/vehicles/${id}`)
    const { typist, dialog } = await openDialog()

    await typist.clear(within(dialog).getByLabelText('Km do painel'))
    await typist.type(within(dialog).getByLabelText('Km do painel'), '216000')
    const save = within(dialog).getByRole('button', { name: 'Salvar km' })
    await typist.click(save)
    await waitFor(() => expect(save).toBeDisabled())
    await typist.click(save)
    expect(sent(calls, 'POST', `/api/vehicles/${id}/odometer`)).toHaveLength(1)

    release()
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('Esc fecha a janela e o foco volta ao botão que a abriu', async () => {
    fakeApi()
    renderApp(`/vehicles/${id}`)
    const { typist, dialog } = await openDialog()
    expect(within(dialog).getByLabelText('Km do painel')).toHaveFocus()

    await typist.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Atualizar km' })).toHaveFocus()
  })
})
