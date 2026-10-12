import type { ReactNode } from 'react'

type Props = {
  title: string
  /** Explicação curta do que fazer. */
  children?: ReactNode
  /** Botões da tela vazia (um por linha no celular). */
  actions?: ReactNode
}

/** Estado vazio: onde ainda não há nada, diz o motivo e põe a ação logo à vista. */
export function Empty({ title, children, actions }: Props) {
  return (
    <section className="rounded-xl border-2 border-dashed border-slate-300 bg-white px-4 py-8 text-center sm:px-8">
      <span className="mx-auto flex size-14 items-center justify-center rounded-full bg-amber-100 text-amber-700">
        <svg
          viewBox="0 0 24 24"
          className="size-7"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.75"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
        >
          <path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.4 2.9A3.7 3.7 0 0 0 2 12v4c0 .6.4 1 1 1h2" />
          <circle cx="7" cy="17" r="2" />
          <path d="M9 17h6" />
          <circle cx="17" cy="17" r="2" />
        </svg>
      </span>
      <h2 className="mt-4 text-lg font-semibold">{title}</h2>
      {children && <div className="mx-auto mt-2 max-w-md text-slate-600">{children}</div>}
      {actions && <div className="mx-auto mt-6 flex max-w-xs flex-col gap-3">{actions}</div>}
    </section>
  )
}
