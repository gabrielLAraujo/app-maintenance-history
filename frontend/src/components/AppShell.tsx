import { Suspense, useState, type ReactNode } from 'react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router'
import { useDueReminders } from '../api/hooks/reminders'
import { useAuth } from '../auth/AuthProvider'
import { SectionLoading } from '../auth/RouteGuards'
import { DemoBanner } from './DemoBanner'
import { Logo } from './Logo'
import { Spinner } from './Spinner'

function NavIcon({ children }: { children: ReactNode }) {
  return (
    <svg
      viewBox="0 0 24 24"
      className="size-[18px] flex-none"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {children}
    </svg>
  )
}

const navItemClass = ({ isActive }: { isActive: boolean }) =>
  `flex min-h-11 items-center gap-3 rounded-control px-3 transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber ${
    isActive ? 'bg-[#262A33] font-semibold text-white [&>svg]:text-amber' : 'font-medium text-[#C9CED8] hover:bg-white/5 hover:text-white'
  }`

/** Quantos lembretes venceram, para o contador do menu. */
function useOverdueCount(): number {
  const due = useDueReminders()
  return due.data?.filter((item) => item.status.state === 'overdue').length ?? 0
}

const overdueLabel = (count: number) => (count === 1 ? '1 vencido' : `${count} vencidos`)

/**
 * Moldura das telas logadas. A partir de 1024 px, barra lateral grafite fixa; abaixo disso, barra superior com o botão
 * "Menu", que abre a mesma navegação. A conta demo ganha a faixa de aviso e aparece como "Visitante" (o e-mail técnico
 * dela nunca é mostrado).
 */
export function AppShell() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [menuOpen, setMenuOpen] = useState(false)
  const [leaving, setLeaving] = useState(false)
  const overdue = useOverdueCount()
  // Escolher um destino fecha o menu do celular.
  const closeMenu = () => setMenuOpen(false)

  async function handleLogout() {
    // O visitante da demo volta à landing (a conta dele acabou); a conta comum vai para o login. Sem o "de onde veio" do
    // RequireAuth: quem sai por vontade própria volta ao início quando entrar de novo.
    const destination = user?.isDemo ? '/' : '/login'
    setLeaving(true)
    await logout(destination)
    navigate(destination, { replace: true })
  }

  return (
    <div className="min-h-dvh lg:flex">
      <header className="sticky top-0 z-30 flex items-center gap-3 bg-ink px-4 py-3 lg:hidden">
        <Link to="/" aria-label="Garagem, painel" className="rounded-control focus-visible:outline-2 focus-visible:outline-amber">
          <Logo inverse />
        </Link>
        <button
          type="button"
          aria-expanded={menuOpen}
          aria-controls="menu-do-app"
          onClick={() => setMenuOpen((open) => !open)}
          className="ml-auto inline-flex min-h-11 items-center gap-2 rounded-control px-3 font-semibold text-white ring-1 ring-white/30 ring-inset hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-amber"
        >
          <svg viewBox="0 0 24 24" className="size-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
            {menuOpen ? <path d="M6 6l12 12M18 6L6 18" /> : <path d="M4 7h16M4 12h16M4 17h16" />}
          </svg>
          Menu
        </button>
      </header>

      <aside
        id="menu-do-app"
        className={`${menuOpen ? 'flex' : 'hidden'} flex-col gap-6 bg-ink px-4 pt-2 pb-5 text-[#E8EAEE] lg:sticky lg:top-0 lg:flex lg:h-dvh lg:w-[248px] lg:flex-none lg:pt-5`}
      >
        <Link
          to="/"
          aria-label="Garagem, painel"
          className="hidden self-start rounded-control px-2 py-1 focus-visible:outline-2 focus-visible:outline-amber lg:inline-flex"
        >
          <Logo inverse />
        </Link>
        <nav aria-label="Principal" className="flex flex-col gap-1">
          <NavLink to="/" end className={navItemClass} onClick={closeMenu}>
            <NavIcon>
              <rect x="3" y="3" width="7" height="7" rx="1.5" />
              <rect x="14" y="3" width="7" height="7" rx="1.5" />
              <rect x="3" y="14" width="7" height="7" rx="1.5" />
              <rect x="14" y="14" width="7" height="7" rx="1.5" />
            </NavIcon>
            Painel
          </NavLink>
          <NavLink to="/vehicles" className={navItemClass} onClick={closeMenu}>
            <NavIcon>
              <path d="M5 16V12l2-4.2A2 2 0 0 1 8.8 6.6h6.4A2 2 0 0 1 17 7.8L19 12v4" />
              <path d="M3.5 16h17v2.5a1 1 0 0 1-1 1H18a1 1 0 0 1-1-1V18H7v.5a1 1 0 0 1-1 1H4.5a1 1 0 0 1-1-1z" />
              <path d="M5 12h14" />
            </NavIcon>
            Veículos
          </NavLink>
          <NavLink
            to="/reminders"
            className={navItemClass}
            onClick={closeMenu}
            // O contador é desenho; o nome do link diz quantos venceram.
            aria-label={overdue > 0 ? `Lembretes, ${overdueLabel(overdue)}` : undefined}
          >
            <NavIcon>
              <path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" />
              <path d="M10.3 21a1.94 1.94 0 0 0 3.4 0" />
            </NavIcon>
            Lembretes
            {overdue > 0 && (
              <span
                aria-hidden="true"
                className="ml-auto inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-[#F04438] px-1.5 text-xs font-bold text-white"
              >
                {overdue}
              </span>
            )}
          </NavLink>
        </nav>

        <div className="flex flex-col gap-2.5 border-t border-[#2E333D] px-2 pt-4 lg:mt-auto">
          {user?.isDemo && (
            <span className="eyebrow self-start rounded-chip bg-[#3A2A06] px-2 py-0.5 text-[11px] font-semibold text-[#F5C26B]">
              Demo
            </span>
          )}
          <span className="truncate text-sm text-[#C9CED8]">{user?.isDemo ? 'Visitante' : user?.email}</span>
          <button
            type="button"
            onClick={() => void handleLogout()}
            disabled={leaving}
            className="inline-flex min-h-11 items-center gap-2 self-start rounded-control text-sm font-medium text-[#C9CED8] hover:text-white focus-visible:outline-2 focus-visible:outline-amber disabled:opacity-60"
          >
            {leaving ? (
              <Spinner size="sm" label={null} />
            ) : (
              <svg viewBox="0 0 24 24" className="size-4" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
                <path d="M16 17l5-5-5-5" />
                <path d="M21 12H9" />
              </svg>
            )}
            Sair
          </button>
        </div>
      </aside>

      <div className="min-w-0 flex-1">
        <main className="mx-auto max-w-[1120px] px-4 py-6 sm:px-6 lg:px-10 lg:py-7">
          {user?.isDemo && user.demoExpiresAt && <DemoBanner expiresAt={user.demoExpiresAt} />}
          <Suspense fallback={<SectionLoading />}>
            <Outlet />
          </Suspense>
        </main>
      </div>
    </div>
  )
}
