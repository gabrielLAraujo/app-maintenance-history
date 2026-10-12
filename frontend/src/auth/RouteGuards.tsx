import { Navigate, Outlet, useLocation } from 'react-router'
import { Spinner } from '../components/Spinner'
import { useAuth } from './AuthProvider'

function PageLoading() {
  return (
    <div className="grid min-h-dvh place-items-center text-slate-500">
      <Spinner />
    </div>
  )
}

/** Rotas que exigem login. Sem sessão (nunca houve ou expirou), vai para /login e guarda de onde veio. */
export function RequireAuth() {
  const { user, loading } = useAuth()
  const location = useLocation()

  if (loading) return <PageLoading />
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />

  return <Outlet />
}

// Só aceita caminho interno do app (o `from` que o RequireAuth gravou); qualquer outra coisa volta ao início.
function destinationAfterLogin(state: unknown): string {
  const from = (state as { from?: unknown } | null)?.from
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') ? from : '/'
}

/** Rotas de visitante (/login, /register): quem já está logado vai para o app. Também dá a moldura centralizada. */
export function GuestOnly() {
  const { user, loading } = useAuth()
  const location = useLocation()

  if (loading) return <PageLoading />
  if (user) return <Navigate to={destinationAfterLogin(location.state)} replace />

  return (
    <main className="mx-auto flex min-h-dvh max-w-sm flex-col justify-center gap-6 px-4 py-8">
      <p className="text-center text-2xl font-bold tracking-tight">Garagem</p>
      <Outlet />
    </main>
  )
}
