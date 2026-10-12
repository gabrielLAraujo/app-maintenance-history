import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { json, mockFetch, problem } from '../../test/fetch-mock'
import { FipePicker } from './FipePicker'

const makes = [
  { id: '21', name: 'Fiat' },
  { id: '25', name: 'Honda' },
  { id: '59', name: 'VW - VolksWagen' },
]

const hondaModels = [
  { id: '4828', fullName: 'Civic Sedan LX 1.6 16V Mec. 4p', suggestedModel: 'Civic', suggestedVersion: 'Sedan LX 1.6 16V Mec. 4p' },
  { id: '4829', fullName: 'Fit EX 1.5 16V Aut.', suggestedModel: 'Fit', suggestedVersion: 'EX 1.5 16V Aut.' },
]

const civicYears = [
  { id: '1997-1', modelYear: 1997, fuel: 'gasoline', label: '1997 Gasolina' },
  { id: '1998-1', modelYear: 1998, fuel: 'gasoline', label: '1998 Gasolina' },
]

const civicDetails = {
  fipeCode: '005340-6',
  make: 'Honda',
  fullName: 'Civic Sedan LX 1.6 16V Mec. 4p',
  modelYear: 1998,
  fuel: 'gasoline',
  price: 23456.78,
  referenceMonth: 'outubro de 2026',
}

/** Catálogo falso da FIPE. Cada teste troca só a rota que quer mudar. */
function fakeCatalog(overrides: Record<string, () => Response> = {}) {
  return mockFetch({
    'GET /api/catalog/car/makes': () => json(makes),
    'GET /api/catalog/car/makes/25/models': () => json(hondaModels),
    'GET /api/catalog/car/makes/25/models/4828/years': () => json(civicYears),
    'GET /api/catalog/car/makes/25/models/4828/years/1998-1': () => json(civicDetails),
    ...overrides,
  })
}

function renderPicker() {
  const onPick = vi.fn()
  const onManual = vi.fn()
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })

  render(
    <QueryClientProvider client={queryClient}>
      <FipePicker onPick={onPick} onManual={onManual} />
    </QueryClientProvider>,
  )

  return { onPick, onManual }
}

const unavailable = () =>
  problem(503, {
    code: 'catalog.unavailable',
    detail: 'Catálogo FIPE indisponível no momento. Preencha manualmente.',
  })

async function pickCivic1998(typist: ReturnType<typeof userEvent.setup>) {
  await typist.click(screen.getByRole('button', { name: 'Carro' }))
  await typist.click(await screen.findByRole('button', { name: 'Honda' }))
  await typist.click(await screen.findByRole('button', { name: 'Civic Sedan LX 1.6 16V Mec. 4p' }))
  await typist.click(await screen.findByRole('button', { name: '1998 Gasolina' }))
}

describe('FipePicker', () => {
  it('Honda → Civic Sedan LX → 1998 Gasolina entrega marca, modelo sugerido, versão, ano, combustível e o vínculo FIPE', async () => {
    fakeCatalog()
    const { onPick, onManual } = renderPicker()

    await pickCivic1998(userEvent.setup())

    await vi.waitFor(() => expect(onPick).toHaveBeenCalledTimes(1))
    expect(onPick).toHaveBeenCalledWith({
      make: 'Honda',
      model: 'Civic',
      version: 'Sedan LX 1.6 16V Mec. 4p',
      modelYear: 1998,
      fuel: 'gasoline',
      catalog: { type: 'car', fipeCode: '005340-6', fipeYearCode: '1998-1' },
    })
    expect(onManual).not.toHaveBeenCalled()
  })

  it('catálogo fora do ar (503) abre o modo manual direto, sem pedir nada ao usuário', async () => {
    fakeCatalog({ 'GET /api/catalog/car/makes': unavailable })
    const { onPick, onManual } = renderPicker()

    await userEvent.click(screen.getByRole('button', { name: 'Carro' }))

    await vi.waitFor(() => expect(onManual).toHaveBeenCalledTimes(1))
    expect(onManual).toHaveBeenCalledWith('unavailable')
    expect(onPick).not.toHaveBeenCalled()
  })

  it('503 ao buscar os detalhes do ano também cai no modo manual', async () => {
    fakeCatalog({ 'GET /api/catalog/car/makes/25/models/4828/years/1998-1': unavailable })
    const { onPick, onManual } = renderPicker()

    await pickCivic1998(userEvent.setup())

    await vi.waitFor(() => expect(onManual).toHaveBeenCalledWith('unavailable'))
    expect(onPick).not.toHaveBeenCalled()
  })

  it('outro erro do catálogo mostra o aviso e deixa tentar de novo, sem abrir o modo manual', async () => {
    let attempts = 0
    fakeCatalog({
      'GET /api/catalog/car/makes': () => {
        attempts += 1
        return attempts === 1 ? problem(500, { detail: 'Algo deu errado do nosso lado.' }) : json(makes)
      },
    })
    const { onManual } = renderPicker()
    const typist = userEvent.setup()

    await typist.click(screen.getByRole('button', { name: 'Carro' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Algo deu errado do nosso lado.')
    expect(onManual).not.toHaveBeenCalled()

    await typist.click(screen.getByRole('button', { name: 'Tentar de novo' }))
    expect(await screen.findByRole('button', { name: 'Honda' })).toBeInTheDocument()
  })

  it('"Não achei, preencher manualmente" está em todos os passos e chama onManual() sem motivo', async () => {
    fakeCatalog()
    const { onManual } = renderPicker()
    const typist = userEvent.setup()
    const manual = () => screen.getByRole('button', { name: 'Não achei, preencher manualmente' })

    expect(manual()).toBeInTheDocument() // tipo
    await typist.click(screen.getByRole('button', { name: 'Carro' }))
    await screen.findByRole('button', { name: 'Honda' })
    expect(manual()).toBeInTheDocument() // marca
    await typist.click(screen.getByRole('button', { name: 'Honda' }))
    await screen.findByRole('button', { name: 'Fit EX 1.5 16V Aut.' })
    expect(manual()).toBeInTheDocument() // modelo
    await typist.click(screen.getByRole('button', { name: 'Civic Sedan LX 1.6 16V Mec. 4p' }))
    await screen.findByRole('button', { name: '1997 Gasolina' })
    expect(manual()).toBeInTheDocument() // ano

    await typist.click(manual())
    expect(onManual).toHaveBeenCalledTimes(1)
    expect(onManual).toHaveBeenCalledWith()
  })

  it('a busca de cada passo filtra por texto, sem diferenciar maiúsculas nem acentos', async () => {
    fakeCatalog()
    renderPicker()
    const typist = userEvent.setup()

    await typist.click(screen.getByRole('button', { name: 'Carro' }))
    await screen.findByRole('button', { name: 'Fiat' })

    await typist.type(screen.getByLabelText('Buscar marca'), 'VOLKS')
    expect(screen.getByRole('button', { name: 'VW - VolksWagen' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Honda' })).not.toBeInTheDocument()

    await typist.clear(screen.getByLabelText('Buscar marca'))
    await typist.type(screen.getByLabelText('Buscar marca'), 'zzz')
    expect(screen.getByText('Nenhuma marca encontrada.')).toBeInTheDocument()
  })

  it('o caminho já escolhido aparece e permite voltar a um passo anterior', async () => {
    fakeCatalog()
    renderPicker()
    const typist = userEvent.setup()

    await typist.click(screen.getByRole('button', { name: 'Carro' }))
    await typist.click(await screen.findByRole('button', { name: 'Honda' }))
    await screen.findByRole('button', { name: 'Fit EX 1.5 16V Aut.' })

    const path = screen.getByRole('navigation', { name: 'Escolhas até aqui' })
    expect(within(path).getByRole('button', { name: /Carro/ })).toBeInTheDocument()
    await typist.click(within(path).getByRole('button', { name: /Honda/ }))

    // Voltou à lista de marcas.
    expect(await screen.findByRole('button', { name: 'Fiat' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Fit EX 1.5 16V Aut.' })).not.toBeInTheDocument()
  })
})
