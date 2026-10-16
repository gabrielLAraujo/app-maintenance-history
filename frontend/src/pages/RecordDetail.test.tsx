import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { json, mockFetch, noContent, problem, resetCookies, type Call, type Handler } from '../test/fetch-mock'
import { renderApp } from '../test/renderApp'
import { signedInRoutes } from '../test/signedIn'

const id = '0198a1c2-0000-7000-8000-0000000000c1'
const recordId = '0198a1c2-0000-7000-8000-0000000000e1'
const recordPath = `/api/vehicles/${id}/records/${recordId}`
const uploadPath = `${recordPath}/attachments`

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

const photo = {
  id: '0198a1c2-0000-7000-8000-0000000000b1',
  kind: 'photo',
  isPublic: true,
  fileName: 'coilover.jpg',
  contentType: 'image/jpeg',
  sizeBytes: 482000,
  uploadedAt: '2025-08-12T15:00:00+00:00',
}

const invoice = {
  id: '0198a1c2-0000-7000-8000-0000000000b2',
  kind: 'document',
  isPublic: false,
  fileName: 'nota-fiscal.pdf',
  contentType: 'application/pdf',
  sizeBytes: 1258291,
  uploadedAt: '2025-08-12T15:01:00+00:00',
}

const coilover = {
  id: recordId,
  type: 'modification',
  date: '2025-08-12',
  odometer: 214800,
  title: 'Coilover Tein Street',
  description: 'Regulado na altura mais baixa.',
  category: 'suspension',
  performedBy: 'Oficina do Zé',
  laborCost: 300,
  replacedOriginalPart: 'Amortecedores originais guardados',
  parts: [
    { name: 'Coilover Tein Street', brand: 'Tein', partNumber: 'GSE90', quantity: 1, unit: 'unit', unitPrice: 4250, lineTotal: 4250 },
    { name: 'Óleo do amortecedor', brand: null, partNumber: null, quantity: 0.5, unit: 'liter', unitPrice: null, lineTotal: 0 },
  ],
  totalCost: 4550,
  attachments: [photo, invoice],
  rowVersion: 3,
}

function fakeApi(routes: Record<string, Handler> = {}) {
  return mockFetch({
    ...signedInRoutes(),
    'GET /api/vehicles': () => json([]),
    'GET /api/reminders/due': () => json([]),
    [`GET /api/vehicles/${id}`]: () => json(civicDto),
    [`GET /api/vehicles/${id}/records`]: () => json({ items: [], nextCursor: null }),
    [`GET ${recordPath}`]: () => json(coilover),
    ...routes,
  })
}

const sent = (calls: Call[], method: string, path: string) =>
  calls.filter((call) => call.method === method && call.path === path)

const bodyOf = (call: Call | undefined) => JSON.parse(call?.body ?? 'null') as unknown

/** Arquivo de teste: o tamanho declarado vale, sem alocar bytes de verdade. */
function fileOf(name: string, type: string, size = 1024) {
  const file = new File(['x'], name, { type })
  Object.defineProperty(file, 'size', { value: size })
  return file
}

const attachment = (n: number, extra: Record<string, unknown> = {}) => ({
  ...photo,
  id: `0198a1c2-0000-7000-8000-00000000f${String(n).padStart(3, '0')}`,
  fileName: `foto-${n}.jpg`,
  ...extra,
})

/**
 * O que o código pôs em cada FormData, na ordem em que foram montados (nome do arquivo e `kind`). O multipart não dá
 * para ler de volta da Request no jsdom (ver fetch-mock.ts), então a conferência é feita aqui, na montagem.
 */
function captureForms() {
  const forms = new Map<FormData, { file?: string; kind?: string }>()
  const append = FormData.prototype.append
  vi.spyOn(FormData.prototype, 'append').mockImplementation(function (this: FormData, ...args: Parameters<typeof append>) {
    const [name, value] = args
    const entry = forms.get(this) ?? {}
    if (name === 'file') entry.file = (value as File).name
    if (name === 'kind') entry.kind = String(value)
    forms.set(this, entry)
    // Repassa os argumentos como vieram: o undici confere `arguments.length`, e um `undefined` a mais já quebra.
    return append.apply(this, args)
  })
  return () => [...forms.values()]
}

beforeEach(() => {
  resetCookies()
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('detalhe do registro', () => {
  it('mostra os dados, as peças com subtotal e o total', async () => {
    fakeApi()
    renderApp(`/vehicles/${id}/records/${recordId}`)

    expect(await screen.findByRole('heading', { level: 1, name: 'Coilover Tein Street' })).toBeInTheDocument()
    expect(screen.getByText('Modificação')).toBeInTheDocument()
    expect(screen.getByText('Suspensão')).toBeInTheDocument()
    expect(screen.getByText(/12\/08\/2025/)).toBeInTheDocument()
    expect(screen.getByText('214.800 km')).toBeInTheDocument()
    expect(screen.getByText('Regulado na altura mais baixa.')).toBeInTheDocument()
    expect(screen.getByText('Oficina do Zé')).toBeInTheDocument()
    expect(screen.getByText('Amortecedores originais guardados')).toBeInTheDocument()

    const costs = screen.getByRole('region', { name: 'Peças e custo' })
    expect(within(costs).getByText('Coilover Tein Street')).toBeInTheDocument()
    expect(within(costs).getByText('Tein · Cód. GSE90')).toBeInTheDocument()
    expect(within(costs).getByText('1 un × R$ 4.250,00')).toBeInTheDocument()
    expect(within(costs).getByText('R$ 4.250,00')).toBeInTheDocument() // subtotal da peça
    // Peça sem preço mostra só a quantidade e não inventa subtotal.
    expect(within(costs).getByText('0,5 L')).toBeInTheDocument()
    expect(within(costs).getByText('R$ 300,00')).toBeInTheDocument() // mão de obra
    expect(within(costs).getByText('R$ 4.550,00')).toBeInTheDocument() // total

    expect(screen.getByRole('link', { name: 'Editar' })).toHaveAttribute('href', `/vehicles/${id}/records/${recordId}/edit`)
    expect(screen.getByRole('link', { name: /Civic EK/ })).toHaveAttribute('href', `/vehicles/${id}`)
  })

  it('registro sem km, sem peças e sem custo não mostra seções vazias', async () => {
    fakeApi({
      [`GET ${recordPath}`]: () =>
        json({
          ...coilover,
          type: 'maintenance',
          odometer: null,
          description: null,
          performedBy: null,
          laborCost: null,
          replacedOriginalPart: null,
          parts: [],
          totalCost: 0,
          attachments: [],
        }),
    })
    renderApp(`/vehicles/${id}/records/${recordId}`)

    expect(await screen.findByText(/sem km informada/)).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: /Custo|Peças/ })).not.toBeInTheDocument()
    expect(screen.queryByText('Quem fez')).not.toBeInTheDocument()
    expect(screen.getByText(/Nenhum anexo ainda/)).toBeInTheDocument()
  })

  it('registro que não existe mostra a tela própria, com o caminho de volta', async () => {
    fakeApi({ [`GET ${recordPath}`]: () => problem(404, { code: 'record.not_found', detail: 'Registro não encontrado.' }) })
    renderApp(`/vehicles/${id}/records/${recordId}`)

    expect(await screen.findByRole('heading', { name: 'Registro não encontrado' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Voltar ao veículo' })).toHaveAttribute('href', `/vehicles/${id}`)
  })

  it('exclui o registro depois de confirmar (avisando dos anexos) e volta ao veículo', async () => {
    const calls = fakeApi({ [`DELETE ${recordPath}`]: () => noContent() })
    renderApp(`/vehicles/${id}/records/${recordId}`)
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Excluir registro' }))
    const dialog = await screen.findByRole('dialog', { name: 'Excluir este registro?' })
    expect(dialog).toHaveTextContent('Coilover Tein Street será apagado de vez, com os 2 anexos.')
    expect(sent(calls, 'DELETE', recordPath)).toHaveLength(0) // só depois de confirmar

    await typist.click(within(dialog).getByRole('button', { name: 'Excluir de vez' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Civic EK' })).toBeInTheDocument()
    expect(sent(calls, 'DELETE', recordPath)[0].headers.get('X-XSRF-TOKEN')).toBe('test-token')
  })

  it('cancelar a exclusão não apaga nada', async () => {
    const calls = fakeApi()
    renderApp(`/vehicles/${id}/records/${recordId}`)
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Excluir registro' }))
    await typist.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancelar' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(sent(calls, 'DELETE', recordPath)).toHaveLength(0)
  })
})

describe('galeria de anexos', () => {
  it('fotos em miniatura (da API, com sessão) e documentos como link; só a foto tem "No link público"', async () => {
    fakeApi()
    renderApp(`/vehicles/${id}/records/${recordId}`)

    const image = await screen.findByRole('img', { name: 'Foto: coilover.jpg' })
    expect(image).toHaveAttribute('src', `/api/vehicles/${id}/attachments/${photo.id}/content`)
    expect(image).toHaveAttribute('loading', 'lazy')
    expect(screen.getByRole('link', { name: 'Abrir a foto coilover.jpg' })).toHaveAttribute('target', '_blank')

    const document = screen.getByRole('link', { name: 'nota-fiscal.pdf' })
    expect(document).toHaveAttribute('href', `/api/vehicles/${id}/attachments/${invoice.id}/content`)
    expect(screen.getByText(/PDF · 1,2 MB/)).toBeInTheDocument()

    expect(screen.getAllByRole('switch')).toHaveLength(1)
    expect(screen.getByRole('switch', { name: 'No link público' })).toBeChecked()
    expect(screen.getByText('2 de 20')).toBeInTheDocument()
  })

  it('alternar "No link público" manda o PATCH e a foto passa a privada', async () => {
    let isPublic = true
    const calls = fakeApi({
      [`GET ${recordPath}`]: () => json({ ...coilover, attachments: [{ ...photo, isPublic }, invoice] }),
      [`PATCH /api/vehicles/${id}/attachments/${photo.id}`]: () => {
        isPublic = false
        return noContent()
      },
    })
    renderApp(`/vehicles/${id}/records/${recordId}`)
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('switch', { name: 'No link público' }))

    await waitFor(() => expect(screen.getByRole('switch', { name: 'No link público' })).not.toBeChecked())
    const [patch] = sent(calls, 'PATCH', `/api/vehicles/${id}/attachments/${photo.id}`)
    expect(patch.headers.get('X-XSRF-TOKEN')).toBe('test-token')
    expect(bodyOf(patch)).toEqual({ isPublic: false })
  })

  it('exclui um anexo só depois de confirmar, e ele some da galeria', async () => {
    let attachments = [photo, invoice]
    const calls = fakeApi({
      [`GET ${recordPath}`]: () => json({ ...coilover, attachments }),
      [`DELETE /api/vehicles/${id}/attachments/${invoice.id}`]: () => {
        attachments = [photo]
        return noContent()
      },
    })
    renderApp(`/vehicles/${id}/records/${recordId}`)
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Excluir o documento nota-fiscal.pdf' }))
    const dialog = await screen.findByRole('dialog', { name: 'Excluir este anexo?' })
    expect(dialog).toHaveTextContent('nota-fiscal.pdf')
    expect(sent(calls, 'DELETE', `/api/vehicles/${id}/attachments/${invoice.id}`)).toHaveLength(0)

    await typist.click(within(dialog).getByRole('button', { name: 'Excluir anexo' }))

    await waitFor(() => expect(screen.queryByRole('link', { name: 'nota-fiscal.pdf' })).not.toBeInTheDocument())
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByText('1 de 20')).toBeInTheDocument()
  })
})

describe('envio de anexos', () => {
  it('dois botões: "Tirar foto" (câmera traseira) e "Escolher arquivo"; Foto/Documento antes de enviar', async () => {
    fakeApi()
    renderApp(`/vehicles/${id}/records/${recordId}`)

    expect(await screen.findByRole('button', { name: 'Tirar foto' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Escolher arquivo' })).toBeEnabled()
    const camera = screen.getByLabelText('Foto da câmera')
    expect(camera).toHaveAttribute('capture', 'environment')
    expect(camera).toHaveAttribute('accept', 'image/jpeg,image/png,image/webp')

    const picker = screen.getByLabelText('Arquivos do aparelho')
    expect(picker).toHaveAttribute('multiple')
    expect(picker).not.toHaveAttribute('accept', expect.stringContaining('pdf'))

    expect(screen.getByRole('radio', { name: 'Foto' })).toBeChecked()
    expect(screen.getByText(/Fotos entram no link público/)).toBeInTheDocument()

    await userEvent.setup().click(screen.getByRole('radio', { name: 'Documento' }))
    expect(screen.getByText(/nunca aparecem no link público/)).toBeInTheDocument()
    expect(screen.getByLabelText('Arquivos do aparelho')).toHaveAttribute('accept', expect.stringContaining('application/pdf'))
  })

  it('vários arquivos vão um por vez (nunca em paralelo), com o tipo escolhido, como multipart com o antiforgery', async () => {
    let inFlight = 0
    let maxInFlight = 0
    let posted = 0
    const forms = captureForms()
    const calls = fakeApi({
      [`POST ${uploadPath}`]: async () => {
        inFlight += 1
        maxInFlight = Math.max(maxInFlight, inFlight)
        posted += 1
        const number = posted
        await new Promise((resolve) => setTimeout(resolve, 15))
        inFlight -= 1
        return json({ ...invoice, id: `0198a1c2-0000-7000-8000-00000000d00${number}` }, 201)
      },
    })
    renderApp(`/vehicles/${id}/records/${recordId}`)
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('radio', { name: 'Documento' }))
    await typist.upload(screen.getByLabelText('Arquivos do aparelho'), [
      fileOf('nota-1.pdf', 'application/pdf'),
      fileOf('garantia.jpg', 'image/jpeg'),
      fileOf('nota-2.pdf', 'application/pdf'),
    ])

    expect(await screen.findByText('3 arquivos enviados.')).toBeInTheDocument()
    expect(maxInFlight).toBe(1)
    expect(posted).toBe(3)
    // Na ordem em que foram escolhidos, todos com o tipo escolhido (Documento).
    expect(forms()).toEqual([
      { file: 'nota-1.pdf', kind: 'document' },
      { file: 'garantia.jpg', kind: 'document' },
      { file: 'nota-2.pdf', kind: 'document' },
    ])

    const [first] = sent(calls, 'POST', uploadPath)
    expect(first.headers.get('X-XSRF-TOKEN')).toBe('test-token')
    // O Content-Type do multipart (com o boundary) é do navegador, não nosso.
    expect(first.headers.get('Content-Type')).toMatch(/^multipart\/form-data; boundary=/)
  })

  it('arquivo acima de 10 MB nem é enviado; os outros do lote seguem', async () => {
    const forms = captureForms()
    const calls = fakeApi({ [`POST ${uploadPath}`]: () => json({ ...photo, id: '0198a1c2-0000-7000-8000-00000000d001' }, 201) })
    renderApp(`/vehicles/${id}/records/${recordId}`)
    const typist = userEvent.setup()

    await typist.upload(await screen.findByLabelText('Arquivos do aparelho'), [
      fileOf('enorme.jpg', 'image/jpeg', 10 * 1024 * 1024 + 1),
      fileOf('no-limite.jpg', 'image/jpeg', 10 * 1024 * 1024),
    ])

    expect(await screen.findByText('Um arquivo não foi enviado:')).toBeInTheDocument()
    expect(screen.getByText('enorme.jpg')).toBeInTheDocument()
    expect(screen.getByText(/Arquivo maior que 10 MB; não foi enviado\./)).toBeInTheDocument()
    expect(screen.getByText('1 arquivo enviado.')).toBeInTheDocument()
    expect(sent(calls, 'POST', uploadPath)).toHaveLength(1)
    expect(forms()).toEqual([{ file: 'no-limite.jpg', kind: 'photo' }])
  })

  it('não passa de 20 anexos por registro: o que não cabe nem é enviado', async () => {
    const nineteen = Array.from({ length: 19 }, (_, index) => attachment(index + 1))
    const calls = fakeApi({
      [`GET ${recordPath}`]: () => json({ ...coilover, attachments: nineteen }),
      [`POST ${uploadPath}`]: () => json({ ...photo, id: '0198a1c2-0000-7000-8000-00000000d001' }, 201),
    })
    renderApp(`/vehicles/${id}/records/${recordId}`)
    const typist = userEvent.setup()

    await typist.upload(await screen.findByLabelText('Arquivos do aparelho'), [
      fileOf('cabe.jpg', 'image/jpeg'),
      fileOf('nao-cabe.jpg', 'image/jpeg'),
    ])

    expect(await screen.findByText(/O registro já está no limite de 20 anexos; não foi enviado\./)).toBeInTheDocument()
    expect(sent(calls, 'POST', uploadPath)).toHaveLength(1)
  })

  it('com 20 anexos os botões de envio ficam desabilitados e o aviso explica', async () => {
    const twenty = Array.from({ length: 20 }, (_, index) => attachment(index + 1))
    fakeApi({ [`GET ${recordPath}`]: () => json({ ...coilover, attachments: twenty }) })
    renderApp(`/vehicles/${id}/records/${recordId}`)

    expect(await screen.findByText(/já tem 20 anexos, o máximo/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tirar foto' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Escolher arquivo' })).toBeDisabled()
  })

  it('erro de um arquivo (415) mostra o motivo e o lote segue com os próximos', async () => {
    let call = 0
    const calls = fakeApi({
      [`POST ${uploadPath}`]: () => {
        call += 1
        return call === 1
          ? problem(415, { code: 'attachment.unsupported_type', detail: 'Formato não suportado. Envie JPG, PNG, WebP ou PDF.' })
          : json({ ...photo, id: '0198a1c2-0000-7000-8000-00000000d001' }, 201)
      },
    })
    renderApp(`/vehicles/${id}/records/${recordId}`)
    const typist = userEvent.setup()

    await typist.upload(await screen.findByLabelText('Arquivos do aparelho'), [
      fileOf('estranho.jpg', 'image/jpeg'),
      fileOf('bom.jpg', 'image/jpeg'),
    ])

    expect(await screen.findByText(/Formato não suportado/)).toBeInTheDocument()
    expect(screen.getByText('1 arquivo enviado.')).toBeInTheDocument()
    expect(sent(calls, 'POST', uploadPath)).toHaveLength(2)
  })

  it('limite por minuto (429) para o lote: os que faltam não são enviados', async () => {
    const calls = fakeApi({
      [`POST ${uploadPath}`]: () => problem(429, { code: 'rate_limit.exceeded', detail: 'Muitas tentativas. Aguarde um minuto.' }),
    })
    renderApp(`/vehicles/${id}/records/${recordId}`)
    const typist = userEvent.setup()

    await typist.upload(await screen.findByLabelText('Arquivos do aparelho'), [
      fileOf('a.jpg', 'image/jpeg'),
      fileOf('b.jpg', 'image/jpeg'),
      fileOf('c.jpg', 'image/jpeg'),
    ])

    expect(await screen.findByText('3 arquivos não foram enviados:')).toBeInTheDocument()
    expect(screen.getByText(/Muitas tentativas/)).toBeInTheDocument()
    expect(screen.getAllByText(/Não foi enviado: o envio parou antes\./)).toHaveLength(2)
    expect(sent(calls, 'POST', uploadPath)).toHaveLength(1)
  })

  it('mostra "Enviando 1 de 2" e desabilita os botões enquanto envia', async () => {
    const releases: Array<() => void> = []
    fakeApi({
      [`POST ${uploadPath}`]: () =>
        new Promise<Response>((resolve) => {
          releases.push(() => resolve(json({ ...photo, id: `0198a1c2-0000-7000-8000-00000000d00${releases.length}` }, 201)))
        }),
    })
    renderApp(`/vehicles/${id}/records/${recordId}`)
    const typist = userEvent.setup()

    await typist.upload(await screen.findByLabelText('Arquivos do aparelho'), [
      fileOf('primeira.jpg', 'image/jpeg'),
      fileOf('segunda.jpg', 'image/jpeg'),
    ])

    expect(await screen.findByText(/Enviando 1 de 2:/)).toHaveTextContent('primeira.jpg')
    expect(screen.getByRole('button', { name: 'Tirar foto' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Escolher arquivo' })).toBeDisabled()
    await waitFor(() => expect(releases).toHaveLength(1))
    await new Promise((resolve) => setTimeout(resolve, 30))
    expect(releases).toHaveLength(1) // a segunda só começa quando a primeira termina

    releases[0]()
    expect(await screen.findByText(/Enviando 2 de 2:/)).toHaveTextContent('segunda.jpg')
    await waitFor(() => expect(releases).toHaveLength(2))
    releases[1]()

    expect(await screen.findByText('2 arquivos enviados.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tirar foto' })).toBeEnabled()
  })
})

describe('capa do veículo a partir das fotos', () => {
  const coverPath = `/api/vehicles/${id}/cover-photo`

  it('"Usar como capa" numa foto manda o id dela; documento não tem a ação', async () => {
    const calls = fakeApi({ [`PUT ${coverPath}`]: () => noContent() })
    renderApp(`/vehicles/${id}/records/${recordId}`)
    const typist = userEvent.setup()

    const use = await screen.findByRole('button', { name: 'Usar como capa' })
    expect(screen.getAllByRole('button', { name: 'Usar como capa' })).toHaveLength(1)
    await typist.click(use)

    await waitFor(() => expect(sent(calls, 'PUT', coverPath)).toHaveLength(1))
    const [put] = sent(calls, 'PUT', coverPath)
    expect(bodyOf(put)).toEqual({ attachmentId: photo.id })
    expect(put.headers.get('X-XSRF-TOKEN')).toBe('test-token')
  })

  it('a foto que é a capa diz isso e oferece "Remover capa"', async () => {
    const calls = fakeApi({
      [`GET /api/vehicles/${id}`]: () => json({ ...civicDto, coverPhotoId: photo.id }),
      [`PUT ${coverPath}`]: () => noContent(),
    })
    renderApp(`/vehicles/${id}/records/${recordId}`)
    const typist = userEvent.setup()

    expect(await screen.findByText('Capa do veículo')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Usar como capa' })).not.toBeInTheDocument()
    await typist.click(screen.getByRole('button', { name: 'Remover capa' }))

    await waitFor(() => expect(sent(calls, 'PUT', coverPath)).toHaveLength(1))
    expect(bodyOf(sent(calls, 'PUT', coverPath)[0])).toEqual({ attachmentId: null })
  })
})

describe('conta demo', () => {
  it('não oferece envio de arquivos e explica por quê', async () => {
    fakeApi({
      'GET /api/auth/me': () =>
        json({
          id: '0198a1c2-0000-7000-8000-0000000000d1',
          email: 'demo-x@demo.garagem.invalid',
          isDemo: true,
          demoExpiresAt: new Date(Date.now() + 23 * 3_600_000).toISOString(),
        }),
    })
    renderApp(`/vehicles/${id}/records/${recordId}`)

    expect(await screen.findByText('Na demonstração não dá para enviar arquivos.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Tirar foto' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Escolher arquivo' })).not.toBeInTheDocument()
    // As fotos de exemplo continuam lá, com as ações que não enviam nada.
    expect(screen.getByRole('button', { name: 'Usar como capa' })).toBeInTheDocument()
  })
})
