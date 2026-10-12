import { useState } from 'react'
import { Link, Outlet, useNavigate } from 'react-router'
import { useAuth } from '../auth/AuthProvider'
import { Button } from './Button'

/** Moldura das telas logadas: cabeçalho (nome do app, e-mail, Sair) e o conteúdo da rota em uma coluna. */
export function Layout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [leaving, setLeaving] = useState(false)

  async function handleLogout() {
    setLeaving(true)
    await logout()
    // Sem o "de onde veio" do RequireAuth: quem sai por vontade própria volta ao início quando entrar de novo.
    navigate('/login', { replace: true })
  }

  return (
    <div className="min-h-dvh">
      <header className="bg-slate-900 text-white">
        <div className="mx-auto flex max-w-3xl items-center gap-3 px-4 py-3">
          <Link to="/" className="text-lg font-bold tracking-tight">
            Garagem
          </Link>
          <span className="ml-auto min-w-0 truncate text-sm text-slate-300">{user?.email}</span>
          <Button variant="inverse" loading={leaving} onClick={handleLogout} className="shrink-0">
            Sair
          </Button>
        </div>
      </header>
      <main className="mx-auto max-w-3xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
