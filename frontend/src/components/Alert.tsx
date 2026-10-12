import type { ComponentProps } from 'react'

type Variant = 'error' | 'warning' | 'info' | 'success'

const variants: Record<Variant, string> = {
  error: 'bg-red-50 text-red-900 ring-red-200',
  warning: 'bg-amber-50 text-amber-900 ring-amber-200',
  info: 'bg-sky-50 text-sky-900 ring-sky-200',
  success: 'bg-emerald-50 text-emerald-900 ring-emerald-200',
}

type Props = ComponentProps<'div'> & { variant?: Variant }

/** Aviso visível. Erro e aviso são anunciados na hora (role="alert"); os demais, com educação (role="status"). */
export function Alert({ variant = 'info', className = '', ...rest }: Props) {
  return (
    <div
      role={variant === 'error' || variant === 'warning' ? 'alert' : 'status'}
      className={`rounded-lg p-3 text-sm ring-1 ring-inset ${variants[variant]} ${className}`}
      {...rest}
    />
  )
}
