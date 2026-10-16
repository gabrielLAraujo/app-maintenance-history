import { screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { json, mockFetch, resetCookies, type Handler } from '../test/fetch-mock'
import { renderApp } from '../test/renderApp'
import { signedInRoutes } from '../test/signedIn'

const vehicle = (id: string, displayName: string, make: string, model: string, modelYear: number) => ({
  id,
  displayName,
  make,
  model,
  modelYear,
  plate: null,
  currentOdometer: 50000,
  coverPhotoId: null,
  totalCost: 0,
  lastRecordDate: null,
})

const gol = vehicle('v2', 'Volkswagen Gol 2019', 'Volkswagen', 'Gol', 2019)
const civic = vehicle('v1', 'Civic EK', 'Honda', 'Civic', 1998)

function fakeApi(routes: Record<string, Handler> = {}) {
  return mockFetch({ ...signedInRoutes(), 'GET /api/vehicles': () => json([gol, civic]), ...routes })
}

beforeEach(() => {
  resetCookies()
})

describe('página Veículos', () => {
  it('lista os veículos em cards, por nome, com "Novo veículo"', async () => {
    fakeApi()
    renderApp('/vehicles')

    expect(await screen.findByRole('heading', { level: 1, name: 'Veículos' })).toBeInTheDocument()
    const names = (await screen.findAllByRole('heading', { level: 2 })).map((heading) => heading.textContent)
    expect(names).toEqual(['Civic EK', 'Volkswagen Gol 2019'])
    const card = screen.getByRole('heading', { level: 2, name: 'Civic EK' }).closest('a')!
    expect(card).toHaveAttribute('href', '/vehicles/v1')
    expect(within(card).getByText('Adicionar foto de capa')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Novo veículo' })).toHaveAttribute('href', '/vehicles/new')
  })

  it('sem veículos mostra o estado vazio com "Cadastrar veículo"', async () => {
    fakeApi({ 'GET /api/vehicles': () => json([]) })
    renderApp('/vehicles')

    expect(await screen.findByText('Nenhum veículo cadastrado')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Cadastrar veículo' })).toHaveAttribute('href', '/vehicles/new')
  })
})
