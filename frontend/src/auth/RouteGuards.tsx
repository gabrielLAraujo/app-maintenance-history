import { Suspense } from 'react'
import { Link, Navigate, Outlet, useLocation } from 'react-router'
import { Logo } from '../components/Logo'
import { Spinner } from '../components/Spinner'
import { AuthProvider, useAuth } from './AuthProvider'

/**
 * Abre a sessão (AuthProvider) para as telas do app. É a raiz das rotas de quem usa conta; a página pública
 * (/p/:token) fica fora dela de propósito: ali não há sessão, então nada consulta /api/auth/* nem reage a 401.
 */
export function AuthRoot() {
  return (
    <AuthProvider>
      {/* As telas do app carregam sob demanda (lazyPages): enquanto o código chega, o mesmo spinner da sessão. */}
      <Suspense fallback={<PageLoading />}>
        <Outlet />
      </Suspense>
    </AuthProvider>
  )
}

/** Tela inteira com o spinner, enquanto se descobre se há sessão. */
export function PageLoading() {
  return (
    <div className="grid min-h-dvh place-items-center text-muted">
      <Spinner />
    </div>
  )
}

/** Spinner dentro de uma moldura que já está na tela (menu do app, logo do login), enquanto a tela carrega. */
export function SectionLoading() {
  return (
    <div className="grid place-items-center py-16 text-muted">
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
      <Link to="/" aria-label="Garagem, início" className="self-center rounded-control">
        <Logo />
      </Link>
      <Suspense fallback={<SectionLoading />}>
        <Outlet />
      </Suspense>
    </main>
  )
}
