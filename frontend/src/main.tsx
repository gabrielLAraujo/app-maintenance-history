import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import '@fontsource-variable/archivo/wdth.css'
import '@fontsource/ibm-plex-mono/400.css'
import '@fontsource/ibm-plex-mono/500.css'
import '@fontsource/ibm-plex-mono/600.css'
import { shouldRetry } from './api/unwrap'
import './index.css'
import { installChunkReload } from './lib/chunkReload'
import { routes } from './routes'

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: shouldRetry, refetchOnWindowFocus: false } },
})
const router = createBrowserRouter(routes)

// Depois de um deploy, a tela que ainda não tinha sido aberta recarrega a página em vez de quebrar (lazyPages).
installChunkReload()

// A sessão (AuthProvider) vive dentro das rotas do app (AuthRoot), não aqui em cima: a página pública não a usa.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
)
