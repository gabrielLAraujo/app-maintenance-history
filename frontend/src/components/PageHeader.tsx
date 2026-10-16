import type { ReactNode } from 'react'
import { Link } from 'react-router'

type Props = {
  title: string
  /** Link discreto acima do título para a tela de onde se veio. */
  back?: { to: string; label: string }
  /** Botões à direita do título (a ação principal da tela). */
  actions?: ReactNode
  /** Linhas abaixo do título (subtítulo, placa, km). */
  children?: ReactNode
}

/** Cabeçalho de tela: voltar, título (h1) e a ação principal ao lado. */
export function PageHeader({ title, back, actions, children }: Props) {
  return (
    <header className="mb-4">
      {back && (
        <Link to={back.to} className="-ml-1 inline-flex min-h-11 items-center px-1 text-sm font-semibold text-muted hover:text-ink">
          <span aria-hidden="true">←&nbsp;</span>
          {back.label}
        </Link>
      )}
      <div className="flex items-start justify-between gap-3">
        <h1 className="min-w-0 font-display text-3xl leading-tight sm:text-4xl">{title}</h1>
        {actions && <div className="flex shrink-0 gap-2">{actions}</div>}
      </div>
      {children}
    </header>
  )
}
