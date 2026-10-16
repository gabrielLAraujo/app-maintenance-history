import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { json, mockFetch, noContent, problem, resetCookies, type Call, type Handler } from '../../test/fetch-mock'
import { renderApp } from '../../test/renderApp'
import { signedInRoutes, vehicleSummaryRoute } from '../../test/signedIn'

// Relógio parado em 12/10/2026 às 12h em Brasília: "hoje" e as datas de validade ficam determinísticas.
const NOW = new Date('2026-10-12T15:00:00Z')

const id = '0198a1c2-0000-7000-8000-0000000000c1'
const linksPath = `/api/vehicles/${id}/share-links`

const civic = {
  id,
  type: 'car',
  make: 'Honda',
  model: 'Civic',
  version: null,
  generation: null,
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

const token = (letter: string) => letter.repeat(43)

const link = (letter: string, extra: Record<string, unknown> = {}) => ({
  id: `0198a1c2-0000-7000-8000-00000000d00${letter.charCodeAt(0) - 64}`,
  token: token(letter),
  path: `/p/${token(letter)}`,
  label: null,
  includeCosts: false,
  includePlate: false,
  expiresAt: null,
  revokedAt: null,
  createdAt: '2026-10-10T12:00:00+00:00',
  isActive: true,
  ...extra,
})

// Em dia: com rótulo, custos e validade; sem rótulo (o link do demo, sem custos); revogado; e expirado.
const buyer = link('A', {
  label: 'Comprador',
  includeCosts: true,
  expiresAt: '2027-01-01T02:59:59+00:00', // 31/12/2026 23:59:59 em Brasília
  createdAt: '2026-10-11T12:00:00+00:00',
})
const plain = link('B', { includePlate: true })
const revoked = link('C', { label: 'Antigo', revokedAt: '2026-09-01T12:00:00+00:00', isActive: false, createdAt: '2026-08-01T12:00:00+00:00' })
const expired = link('D', {
  label: 'Vencido',
  expiresAt: '2026-10-01T02:59:59+00:00', // 30/09/2026 23:59:59 em Brasília
  isActive: false,
  createdAt: '2026-07-01T12:00:00+00:00',
})

function fakeApi(routes: Record<string, Handler> = {}) {
  return mockFetch({
    ...signedInRoutes(),
    ...vehicleSummaryRoute(id),
    'GET /api/vehicles': () => json([]),
    'GET /api/reminders/due': () => json([]),
    [`GET /api/vehicles/${id}`]: () => json(civic),
    [`GET ${linksPath}`]: () => json([revoked, plain, expired, buyer]),
    ...routes,
  })
}

const open = () => renderApp(`/vehicles/${id}?tab=share`)
const bodyOf = (call: Call | undefined) => JSON.parse(call?.body ?? 'null')
const writes = (calls: Call[], method: string) => calls.filter((call) => call.method === method && call.path.startsWith(linksPath))
const urlOf = (letter: string) => `${window.location.origin}/p/${token(letter)}`

beforeEach(() => {
  resetCookies()
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(NOW)
})

afterEach(() => {
  vi.useRealTimers()
})

describe('lista de links', () => {
  it('ativos primeiro (o mais novo antes), depois os inativos; cada um com status, o que mostra e a validade', async () => {
    fakeApi()
    open()

    const rows = await screen.findAllByRole('listitem')
    expect(rows.map((row) => within(row).getByRole('heading', { level: 3 }).textContent)).toEqual([
      'Comprador',
      'Link sem nome',
      'Antigo',
      'Vencido',
    ])

    expect(within(rows[0]).getByText('Ativo')).toBeInTheDocument()
    expect(within(rows[0]).getByText('Com custos')).toBeInTheDocument()
    expect(within(rows[0]).getByText('Sem placa')).toBeInTheDocument()
    expect(within(rows[0]).getByText('Expira em 31/12/2026')).toBeInTheDocument()
    expect(within(rows[0]).getByText(urlOf('A'))).toBeInTheDocument()

    expect(within(rows[1]).getByText('Sem custos')).toBeInTheDocument()
    expect(within(rows[1]).getByText('Com placa')).toBeInTheDocument()
    expect(within(rows[1]).getByText('Sem validade')).toBeInTheDocument()

    expect(within(rows[2]).getByText('Revogado')).toBeInTheDocument()
    expect(within(rows[2]).getByText('Revogado em 01/09/2026')).toBeInTheDocument()
    expect(within(rows[3]).getByText('Expirado')).toBeInTheDocument()
    expect(within(rows[3]).getByText('Expirou em 30/09/2026')).toBeInTheDocument()
  })

  it('link revogado ou expirado aparece como inativo: sem endereço, sem copiar, abrir nem revogar', async () => {
    fakeApi()
    open()

    const rows = await screen.findAllByRole('listitem')
    for (const row of [rows[2], rows[3]]) {
      expect(within(row).queryByRole('button')).not.toBeInTheDocument()
      expect(within(row).queryByRole('link')).not.toBeInTheDocument()
      expect(within(row).queryByText(/\/p\//)).not.toBeInTheDocument()
    }
  })

  it('"Abrir" leva à página pública em outra aba, sem mandar o endereço de origem', async () => {
    fakeApi()
    open()

    const row = (await screen.findAllByRole('listitem'))[0]
    const open_ = within(row).getByRole('link', { name: 'Abrir Comprador' })
    expect(open_).toHaveAttribute('href', `/p/${token('A')}`)
    expect(open_).toHaveAttribute('target', '_blank')
    expect(open_).toHaveAttribute('rel', expect.stringContaining('noopener'))
    expect(open_).toHaveAttribute('rel', expect.stringContaining('noreferrer'))
  })

  it('explica o que o link mostra e o que nunca mostra', async () => {
    fakeApi()
    open()

    await screen.findAllByRole('listitem')
    expect(screen.getByText(/nunca aparecem/i)).toBeInTheDocument()
  })

  it('sem links mostra o convite para criar o primeiro', async () => {
    fakeApi({ [`GET ${linksPath}`]: () => json([]) })
    open()

    expect(await screen.findByText('Nenhum link ainda')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Criar o primeiro link' })).toBeInTheDocument()
  })

  it('erro ao buscar mostra o motivo e "Tentar de novo" busca outra vez', async () => {
    let attempt = 0
    fakeApi({
      [`GET ${linksPath}`]: () => {
        attempt += 1
        return attempt === 1
          ? problem(500, { code: 'server.error', detail: 'Não foi possível carregar os links agora.' })
          : json([buyer])
      },
    })
    open()
    const typist = userEvent.setup()

    expect(await screen.findByText('Não foi possível carregar os links agora.')).toBeInTheDocument()
    await typist.click(screen.getByRole('button', { name: 'Tentar de novo' }))

    expect(await screen.findByText('Comprador')).toBeInTheDocument()
  })
})

describe('copiar link', () => {
  it('copia o endereço absoluto (origem + caminho) e confirma', async () => {
    fakeApi()
    open()
    const typist = userEvent.setup() // instala um navigator.clipboard de teste

    await typist.click(await screen.findByRole('button', { name: 'Copiar link Comprador' }))

    expect(await navigator.clipboard.readText()).toBe(urlOf('A'))
    expect(await screen.findByRole('button', { name: 'Link copiado' })).toBeInTheDocument()
  })

  it('se o navegador não deixar copiar, mostra o endereço pronto para selecionar', async () => {
    fakeApi()
    open()
    const typist = userEvent.setup()
    vi.spyOn(navigator.clipboard, 'writeText').mockRejectedValue(new Error('negado'))

    await typist.click(await screen.findByRole('button', { name: 'Copiar link Comprador' }))

    expect(await screen.findByText(/Não foi possível copiar/)).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: 'Endereço do link Comprador' })).toHaveValue(urlOf('A'))
  })
})

describe('criar link', () => {
  async function openCreate() {
    const typist = userEvent.setup()
    await typist.click(await screen.findByRole('button', { name: 'Novo link' }))
    const dialog = await screen.findByRole('dialog', { name: 'Novo link público' })
    return { typist, dialog }
  }

  it('abre com tudo desligado: custos e placa só entram se o dono marcar', async () => {
    fakeApi()
    open()

    const { dialog } = await openCreate()

    expect(within(dialog).getByRole('checkbox', { name: /Mostrar custos/ })).not.toBeChecked()
    expect(within(dialog).getByRole('checkbox', { name: /Mostrar placa/ })).not.toBeChecked()
    expect(within(dialog).getByLabelText('Validade (opcional)')).toHaveValue('')
  })

  it('sem preencher nada cria um link sem rótulo, sem custos, sem placa e sem validade (null)', async () => {
    const calls = fakeApi({ [`POST ${linksPath}`]: () => json(link('E'), 201) })
    open()
    const { typist, dialog } = await openCreate()

    await typist.click(within(dialog).getByRole('button', { name: 'Criar link' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(writes(calls, 'POST')[0].headers.get('X-XSRF-TOKEN')).toBe('test-token')
    expect(bodyOf(writes(calls, 'POST')[0])).toEqual({ label: null, includeCosts: false, includePlate: false, expiresAt: null })
  })

  it('manda rótulo, custos, placa e a validade como instante ISO com deslocamento (fim do dia em Brasília)', async () => {
    let created = false
    const calls = fakeApi({
      [`GET ${linksPath}`]: () => json(created ? [link('E', { label: 'Seguradora' }), buyer] : [buyer]),
      [`POST ${linksPath}`]: () => {
        created = true
        return json(link('E', { label: 'Seguradora' }), 201)
      },
    })
    open()
    const { typist, dialog } = await openCreate()

    await typist.type(within(dialog).getByLabelText('Rótulo (opcional)'), '  Seguradora ')
    await typist.click(within(dialog).getByRole('checkbox', { name: /Mostrar custos/ }))
    await typist.click(within(dialog).getByRole('checkbox', { name: /Mostrar placa/ }))
    fireEvent.change(within(dialog).getByLabelText('Validade (opcional)'), { target: { value: '2026-12-31' } })
    await typist.click(within(dialog).getByRole('button', { name: 'Criar link' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(bodyOf(writes(calls, 'POST')[0])).toEqual({
      label: 'Seguradora',
      includeCosts: true,
      includePlate: true,
      expiresAt: '2026-12-31T23:59:59-03:00',
    })

    // A lista busca de novo, o link novo aparece e um aviso diz o que fazer em seguida.
    expect(await screen.findByText('Seguradora')).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Link criado')
  })

  it('validade de hoje vale (o link expira no fim do dia); de ontem é recusada sem chamar a API', async () => {
    const calls = fakeApi({ [`POST ${linksPath}`]: () => json(link('E'), 201) })
    open()
    const { typist, dialog } = await openCreate()
    const validity = within(dialog).getByLabelText('Validade (opcional)')

    fireEvent.change(validity, { target: { value: '2026-10-11' } })
    await typist.click(within(dialog).getByRole('button', { name: 'Criar link' }))
    expect(await within(dialog).findByText('A validade deve ser hoje ou uma data futura.')).toBeInTheDocument()
    expect(writes(calls, 'POST')).toHaveLength(0)

    fireEvent.change(validity, { target: { value: '2026-10-12' } })
    await typist.click(within(dialog).getByRole('button', { name: 'Criar link' }))
    await waitFor(() => expect(writes(calls, 'POST')).toHaveLength(1))
    expect(bodyOf(writes(calls, 'POST')[0]).expiresAt).toBe('2026-10-12T23:59:59-03:00')
  })

  it('rótulo com mais de 60 caracteres é recusado, sem chamar a API', async () => {
    const calls = fakeApi()
    open()
    const { typist, dialog } = await openCreate()

    await typist.type(within(dialog).getByLabelText('Rótulo (opcional)'), 'x'.repeat(61))
    await typist.click(within(dialog).getByRole('button', { name: 'Criar link' }))

    expect(await within(dialog).findByText('O rótulo deve ter no máximo 60 caracteres.')).toBeInTheDocument()
    expect(writes(calls, 'POST')).toHaveLength(0)
  })

  it('erro de validade da API vai para o campo e a janela continua aberta', async () => {
    fakeApi({
      [`POST ${linksPath}`]: () =>
        problem(400, {
          code: 'validation.failed',
          errors: { expiresAt: ['A validade do link deve ser uma data futura.'] },
        }),
    })
    open()
    const { typist, dialog } = await openCreate()

    fireEvent.change(within(dialog).getByLabelText('Validade (opcional)'), { target: { value: '2026-12-31' } })
    await typist.click(within(dialog).getByRole('button', { name: 'Criar link' }))

    expect(await within(dialog).findByText('A validade do link deve ser uma data futura.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Validade (opcional)')).toBeInvalid()
    expect(screen.getByRole('dialog', { name: 'Novo link público' })).toBeInTheDocument()
  })

  it('o botão fica desabilitado enquanto cria (nada de link duplicado)', async () => {
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    const calls = fakeApi({
      [`POST ${linksPath}`]: async () => {
        await gate
        return json(link('E'), 201)
      },
    })
    open()
    const { typist, dialog } = await openCreate()

    const submit = within(dialog).getByRole('button', { name: 'Criar link' })
    await typist.click(submit)
    await waitFor(() => expect(submit).toBeDisabled())
    await typist.click(submit)
    release()

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(writes(calls, 'POST')).toHaveLength(1)
  })
})

describe('revogar link', () => {
  it('pede confirmação; confirmar revoga e o link passa a aparecer como inativo', async () => {
    let done = false
    const calls = fakeApi({
      [`GET ${linksPath}`]: () => json(done ? [{ ...buyer, isActive: false, revokedAt: '2026-10-12T15:00:00+00:00' }] : [buyer]),
      [`DELETE ${linksPath}/${buyer.id}`]: () => {
        done = true
        return noContent()
      },
    })
    open()
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Revogar Comprador' }))
    const dialog = await screen.findByRole('dialog', { name: 'Revogar este link?' })
    expect(within(dialog).getByText('Comprador')).toBeInTheDocument()
    expect(writes(calls, 'DELETE')).toHaveLength(0)

    await typist.click(within(dialog).getByRole('button', { name: 'Revogar link' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(writes(calls, 'DELETE')).toHaveLength(1)
    expect(writes(calls, 'DELETE')[0].headers.get('X-XSRF-TOKEN')).toBe('test-token')
    expect(await screen.findByText('Revogado em 12/10/2026')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Revogar/ })).not.toBeInTheDocument()
  })

  it('o aviso "Link criado" some quando o dono passa a revogar: não fica falando de um link que ele está cancelando', async () => {
    fakeApi({ [`POST ${linksPath}`]: () => json(link('E'), 201) })
    open()
    const typist = userEvent.setup()
    await typist.click(await screen.findByRole('button', { name: 'Novo link' }))
    await typist.click(within(await screen.findByRole('dialog', { name: 'Novo link público' })).getByRole('button', { name: 'Criar link' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Link criado')

    await typist.click(await screen.findByRole('button', { name: 'Revogar Comprador' }))

    expect(await screen.findByRole('dialog', { name: 'Revogar este link?' })).toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('cancelar não revoga nada', async () => {
    const calls = fakeApi()
    open()
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Revogar Comprador' }))
    await typist.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancelar' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(writes(calls, 'DELETE')).toHaveLength(0)
  })

  it('erro ao revogar aparece na janela, que continua aberta', async () => {
    fakeApi({
      [`DELETE ${linksPath}/${buyer.id}`]: () => problem(500, { code: 'server.error', detail: 'Não foi possível revogar agora.' }),
    })
    open()
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Revogar Comprador' }))
    const dialog = await screen.findByRole('dialog')
    await typist.click(within(dialog).getByRole('button', { name: 'Revogar link' }))

    expect(await within(dialog).findByText('Não foi possível revogar agora.')).toBeInTheDocument()
  })
})
