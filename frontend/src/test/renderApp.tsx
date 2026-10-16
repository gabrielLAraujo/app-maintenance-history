import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createMemoryRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { routes } from '../routes'

// As telas logadas carregam sob demanda (lazyPages). O primeiro import dinâmico de cada arquivo de teste transforma a tela
// na hora e, numa máquina carregada (a suíte roda em paralelo), passava do 1 s dos findBy. Importadas aqui de antemão,
// o lazy só espera um tique. O glob pega também as telas que entrarem depois.
import.meta.glob(['../pages/*.tsx', '../components/AppShell.tsx', '!../pages/*.test.tsx'], { eager: true })

/** O app inteiro (providers + rotas reais) numa URL, como o main.tsx monta, mas com router em memória. */
export function renderApp(path = '/') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })

  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )

  return { router, queryClient }
}
