import { cleanup, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { json, mockFetch, problem, resetCookies, type Call, type Handler } from '../test/fetch-mock'
import { renderApp } from '../test/renderApp'

const token = 'T'.repeat(43)
const publicPath = `/api/public/${token}`
const photoPath = (n: number) => `${publicPath}/attachments/0198a1c2-0000-7000-8000-00000000e00${n}`

/** A visão pública de um link SEM custos e SEM placa: os campos sensíveis vêm null, como a API manda. */
const view = (extra: Record<string, unknown> = {}) => ({
  make: 'Honda',
  model: 'Civic',
  version: 'Sedan LX 1.6 16V Mec. 4p',
  generation: 'EK',
  manufactureYear: 1998,
  modelYear: 1998,
  fuel: 'gasoline',
  nickname: 'Civic EK',
  plate: null,
  currentOdometer: 215000,
  includesCosts: false,
  records: [] as unknown[],
  nextCursor: null as string | null,
  ...extra,
})

const record = (extra: Record<string, unknown> = {}) => ({
  type: 'maintenance',
  date: '2026-09-20',
  odometer: 214000,
  category: 'engine',
  title: 'Troca de óleo e filtro',
  description: 'Óleo 5W30 sintético.',
  performedBy: 'Oficina do Zé',
  replacedOriginalPart: null,
  parts: [
    { name: 'Óleo 5W30', brand: 'Mobil', partNumber: null, quantity: 4, unit: 'liter', unitPrice: null },
    { name: 'Filtro de óleo', brand: 'Mann', partNumber: 'W 712/95', quantity: 1, unit: 'unit', unitPrice: null },
  ],
  laborCost: null,
  totalCost: null,
  photos: [] as unknown[],
  ...extra,
})

function fakePublic(handler: Handler) {
  return mockFetch({ [`GET ${publicPath}`]: handler })
}

const open = () => renderApp(`/p/${token}`)
const queryOf = (call: Call) => new URL(call.path, 'http://localhost').searchParams

beforeEach(() => {
  resetCookies()
})

describe('página pública: o que mostra', () => {
  it('veículo, km e registros com peças; sem custos nem placa quando a API não manda', async () => {
    fakePublic(() => json(view({ records: [record()] })))
    open()

    expect(await screen.findByRole('heading', { level: 1, name: 'Civic EK' })).toBeInTheDocument()
    expect(screen.getByText('Honda Civic 1998 · EK · Sedan LX 1.6 16V Mec. 4p · Gasolina')).toBeInTheDocument()
    expect(screen.getByText('215.000 km')).toBeInTheDocument()

    const card = screen.getByRole('article')
    expect(within(card).getByRole('heading', { level: 3, name: 'Troca de óleo e filtro' })).toBeInTheDocument()
    expect(within(card).getByText('20/09/2026 · 214.000 km')).toBeInTheDocument()
    expect(within(card).getByText('Manutenção')).toBeInTheDocument()
    expect(within(card).getByText('Motor')).toBeInTheDocument()
    expect(within(card).getByText('Óleo 5W30 sintético.')).toBeInTheDocument()
    expect(within(card).getByText('Oficina do Zé')).toBeInTheDocument()
    expect(within(card).getByText('Óleo 5W30')).toBeInTheDocument()
    expect(within(card).getByText('Mobil')).toBeInTheDocument()
    expect(within(card).getByText('4 L')).toBeInTheDocument()
    expect(within(card).getByText('Mann · Cód. W 712/95')).toBeInTheDocument()
    expect(within(card).getByText('1 un')).toBeInTheDocument()

    // Nada de dinheiro nem placa: o que a API devolve null a tela não inventa.
    expect(document.body.textContent).not.toMatch(/R\$/)
    expect(screen.queryByText(/Mão de obra/)).not.toBeInTheDocument()
    expect(screen.queryByText('Total')).not.toBeInTheDocument()
    expect(document.querySelector('.font-mono')).toBeNull()
  })

  it('a placa aparece só quando a API a devolve', async () => {
    fakePublic(() => json(view({ plate: 'ABC1D23' })))
    open()

    expect(await screen.findByText('ABC1D23')).toBeInTheDocument()
  })

  it('com custos: preço das peças, mão de obra e total, exatamente como a API manda', async () => {
    fakePublic(() =>
      json(
        view({
          includesCosts: true,
          records: [
            record({
              parts: [
                { name: 'Óleo 5W30', brand: 'Mobil', partNumber: null, quantity: 4, unit: 'liter', unitPrice: 38.9 },
                { name: 'Filtro de óleo', brand: null, partNumber: null, quantity: 1, unit: 'unit', unitPrice: null },
              ],
              laborCost: 120,
              totalCost: 275.6,
            }),
          ],
        }),
      ),
    )
    open()

    const card = await screen.findByRole('article')
    expect(within(card).getByText('4 L × R$ 38,90')).toBeInTheDocument()
    // Peça sem preço (cortesia, por exemplo) fica sem valor; a tela não calcula nem estima.
    expect(within(card).getByText('1 un')).toBeInTheDocument()
    expect(within(card).getByText('Mão de obra')).toBeInTheDocument()
    expect(within(card).getByText('R$ 120,00')).toBeInTheDocument()
    expect(within(card).getByText('Total')).toBeInTheDocument()
    expect(within(card).getByText('R$ 275,60')).toBeInTheDocument()
  })

  it('registro sem custo (total 0) não mostra bloco de valores nem "R$ 0,00"', async () => {
    fakePublic(() => json(view({ includesCosts: true, records: [record({ laborCost: null, totalCost: 0 })] })))
    open()

    await screen.findByRole('article')
    expect(document.body.textContent).not.toMatch(/R\$/)
    expect(screen.queryByText('Total')).not.toBeInTheDocument()
  })

  it('modificação mostra a peça original substituída; registro sem km mostra só a data', async () => {
    fakePublic(() =>
      json(
        view({
          records: [
            record({
              type: 'modification',
              category: 'suspension',
              title: 'Coilover Tein Street',
              odometer: null,
              replacedOriginalPart: 'Amortecedores originais',
              description: null,
              performedBy: null,
            }),
          ],
        }),
      ),
    )
    open()

    const card = await screen.findByRole('article')
    expect(within(card).getByText('Modificação')).toBeInTheDocument()
    expect(within(card).getByText('Amortecedores originais')).toBeInTheDocument()
    expect(within(card).getByText('20/09/2026')).toBeInTheDocument()
    expect(within(card).queryByText(/km/)).not.toBeInTheDocument()
  })

  it('fotos públicas: miniaturas que abrem a imagem em outra aba', async () => {
    fakePublic(() =>
      json(
        view({
          records: [
            record({
              photos: [
                { id: '0198a1c2-0000-7000-8000-00000000e001', path: photoPath(1) },
                { id: '0198a1c2-0000-7000-8000-00000000e002', path: photoPath(2) },
              ],
            }),
          ],
        }),
      ),
    )
    open()

    const photos = await screen.findAllByRole('img')
    expect(photos).toHaveLength(2)
    expect(photos[0]).toHaveAttribute('src', photoPath(1))
    expect(photos[0]).toHaveAttribute('alt', 'Foto 1: Troca de óleo e filtro')
    expect(photos[0]).toHaveAttribute('loading', 'lazy')

    const open_ = photos[0].closest('a')
    expect(open_).toHaveAttribute('href', photoPath(1))
    expect(open_).toHaveAttribute('target', '_blank')
    expect(open_).toHaveAttribute('rel', 'noopener noreferrer')
  })

  it('veículo sem registros diz isso', async () => {
    fakePublic(() => json(view()))
    open()

    expect(await screen.findByText('Nenhum registro neste link.')).toBeInTheDocument()
  })
})

describe('página pública: paginação', () => {
  it('"Carregar mais" segue o nextCursor e acrescenta a página; some quando acaba', async () => {
    const calls = fakePublic((call) =>
      queryOf(call).get('cursor') === 'pagina-2'
        ? json(view({ records: [record({ title: 'Coilover Tein Street', date: '2025-08-12' })], nextCursor: null }))
        : json(view({ records: [record()], nextCursor: 'pagina-2' })),
    )
    open()
    const typist = userEvent.setup()

    expect(await screen.findByText('Troca de óleo e filtro')).toBeInTheDocument()
    expect(screen.queryByText('Coilover Tein Street')).not.toBeInTheDocument()
    expect(queryOf(calls[0]).has('cursor')).toBe(false)

    await typist.click(screen.getByRole('button', { name: 'Carregar mais' }))

    expect(await screen.findByText('Coilover Tein Street')).toBeInTheDocument()
    expect(screen.getByText('Troca de óleo e filtro')).toBeInTheDocument() // a primeira página continua
    expect(queryOf(calls.at(-1)!).get('cursor')).toBe('pagina-2')
    expect(screen.queryByRole('button', { name: 'Carregar mais' })).not.toBeInTheDocument()
    expect(screen.getAllByRole('article')).toHaveLength(2)
  })

  it('falha ao carregar a próxima página avisa e mantém o que já estava na tela', async () => {
    fakePublic((call) =>
      queryOf(call).has('cursor')
        ? problem(500, { code: 'server.error', detail: 'Não foi possível carregar mais agora.' })
        : json(view({ records: [record()], nextCursor: 'pagina-2' })),
    )
    open()
    const typist = userEvent.setup()

    await typist.click(await screen.findByRole('button', { name: 'Carregar mais' }))

    expect(await screen.findByText('Não foi possível carregar mais agora.')).toBeInTheDocument()
    expect(screen.getByText('Troca de óleo e filtro')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Carregar mais' })).toBeInTheDocument()
  })
})

describe('página pública: link inválido', () => {
  const notFound = 'Link não encontrado ou expirado'

  it('404 (inexistente, revogado ou expirado: a API não distingue) mostra o aviso e nada mais', async () => {
    fakePublic(() => problem(404, { code: 'share_link.not_found', detail: 'Link não encontrado ou expirado.' }))
    const { router } = open()

    expect(await screen.findByRole('heading', { level: 1, name: notFound })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Tentar de novo' })).not.toBeInTheDocument()
    expect(router.state.location.pathname).toBe(`/p/${token}`)
  })

  it('qualquer falha mostra o mesmo aviso, sem redirecionar para o login (nem 401)', async () => {
    fakePublic(() => problem(401, { title: 'Unauthorized' }))
    const { router } = open()

    expect(await screen.findByRole('heading', { level: 1, name: notFound })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe(`/p/${token}`)
    expect(screen.queryByText('Entrar')).not.toBeInTheDocument()
  })

  it('erro do servidor ou de rede: o mesmo aviso, com "Tentar de novo" (pode ser passageiro)', async () => {
    let attempt = 0
    fakePublic(() => {
      attempt += 1
      return attempt === 1 ? problem(503, { code: 'server.unavailable' }) : json(view({ records: [record()] }))
    })
    open()
    const typist = userEvent.setup()

    expect(await screen.findByRole('heading', { level: 1, name: notFound })).toBeInTheDocument()
    await typist.click(screen.getByRole('button', { name: 'Tentar de novo' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Civic EK' })).toBeInTheDocument()
    expect(screen.queryByText(notFound)).not.toBeInTheDocument()
  })
})

describe('página pública: anônima', () => {
  it('só chama a rota pública, sem cookie de sessão nem token antiforgery, e não consulta /api/auth', async () => {
    const calls = fakePublic(() => json(view({ records: [record()], nextCursor: 'pagina-2' })))
    open()
    const typist = userEvent.setup()

    await screen.findByRole('heading', { level: 1, name: 'Civic EK' })
    await typist.click(screen.getByRole('button', { name: 'Carregar mais' }))
    await waitFor(() => expect(calls).toHaveLength(2))

    expect(calls.every((call) => call.path.startsWith(publicPath))).toBe(true)
    for (const call of calls) {
      expect(call.method).toBe('GET')
      expect(call.credentials).toBe('omit')
      expect(call.headers.has('X-XSRF-TOKEN')).toBe(false)
    }
  })

  it('pede aos buscadores para não indexar e não vazar o endereço, e tira isso ao sair da página', async () => {
    fakePublic(() => json(view()))
    open()
    await screen.findByRole('heading', { level: 1, name: 'Civic EK' })

    expect(document.head.querySelector('meta[name="robots"]')).toHaveAttribute('content', 'noindex')
    expect(document.head.querySelector('meta[name="referrer"]')).toHaveAttribute('content', 'no-referrer')

    cleanup() // sai da página
    expect(document.head.querySelector('meta[name="robots"]')).toBeNull()
    expect(document.head.querySelector('meta[name="referrer"]')).toBeNull()
  })

  it('o aviso de link inválido também não é indexado', async () => {
    fakePublic(() => problem(404, { code: 'share_link.not_found' }))
    open()
    await screen.findByRole('heading', { level: 1, name: 'Link não encontrado ou expirado' })

    expect(document.head.querySelector('meta[name="robots"]')).toHaveAttribute('content', 'noindex')
  })

  it('não tem o cabeçalho do app (nada de e-mail, "Sair" nem link para o painel)', async () => {
    fakePublic(() => json(view()))
    open()
    await screen.findByRole('heading', { level: 1, name: 'Civic EK' })

    expect(screen.queryByRole('button', { name: 'Sair' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })
})
