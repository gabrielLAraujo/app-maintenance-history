export type ButtonVariant = 'primary' | 'secondary' | 'dark' | 'ghost' | 'danger' | 'inverse'

const variants: Record<ButtonVariant, string> = {
  // Âmbar sempre com texto grafite.
  primary: 'bg-amber font-bold text-ink hover:bg-amber-hover focus-visible:outline-amber-ink',
  secondary:
    'bg-surface font-semibold text-ink ring-1 ring-inset ring-line-strong hover:bg-ground focus-visible:outline-ink',
  dark: 'bg-ink font-semibold text-white hover:bg-ink-2 focus-visible:outline-ink',
  ghost: 'font-semibold text-ink hover:bg-ink/5 focus-visible:outline-ink',
  danger: 'bg-danger font-semibold text-white hover:bg-danger-hover focus-visible:outline-danger',
  // Sobre fundo escuro (a barra lateral, a landing).
  inverse: 'font-semibold text-white ring-1 ring-inset ring-white/30 hover:bg-white/10 focus-visible:outline-white',
}

/** As classes de um botão (44px de altura mínima, alvo de toque): o `<button>` e o link com cara de botão usam as mesmas. */
export const buttonClass = (variant: ButtonVariant = 'primary', className = ''): string =>
  `inline-flex min-h-11 items-center justify-center gap-2 rounded-control px-4 text-base transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 disabled:cursor-not-allowed disabled:opacity-60 ${variants[variant]} ${className}`
