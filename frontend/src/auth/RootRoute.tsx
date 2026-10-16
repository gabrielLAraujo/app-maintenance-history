import { AppShell } from '../components/AppShell'
import { Landing } from '../pages/Landing'
import { useAuth } from './AuthProvider'
import { PageLoading } from './RouteGuards'

/**
 * O início (`/`): visitante vê a landing; quem tem sessão vê o painel dentro da moldura do app. Sem sessão, as outras
 * telas logadas continuam levando ao /login (RequireAuth).
 */
export function RootRoute() {
  const { user, loading } = useAuth()

  if (loading) return <PageLoading />
  if (!user) return <Landing />

  return <AppShell />
}
