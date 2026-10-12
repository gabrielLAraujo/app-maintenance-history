export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'inverse'

const variants: Record<ButtonVariant, string> = {
  primary: 'bg-amber-500 text-slate-900 hover:bg-amber-400 focus-visible:outline-amber-600',
  secondary: 'bg-white text-slate-900 ring-1 ring-inset ring-slate-300 hover:bg-slate-100 focus-visible:outline-slate-500',
  danger: 'bg-red-600 text-white hover:bg-red-500 focus-visible:outline-red-700',
  // Sobre fundo escuro (o cabeçalho).
  inverse: 'text-white ring-1 ring-inset ring-white/30 hover:bg-white/10 focus-visible:outline-white',
}

/** As classes de um botão (44px de altura mínima, alvo de toque): o `<button>` e o link com cara de botão usam as mesmas. */
export const buttonClass = (variant: ButtonVariant = 'primary', className = ''): string =>
  `inline-flex min-h-11 items-center justify-center gap-2 rounded-lg px-4 text-base font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 disabled:cursor-not-allowed disabled:opacity-60 ${variants[variant]} ${className}`
