import type { ComponentProps } from 'react'

type Variant = 'error' | 'warning' | 'info' | 'success'

const variants: Record<Variant, string> = {
  error: 'bg-danger-soft text-danger ring-danger-line',
  warning: 'bg-amber-soft text-amber-ink ring-amber-line',
  info: 'bg-plate-soft text-plate ring-plate-line',
  success: 'bg-ok-soft text-ok ring-ok-line',
}

type Props = ComponentProps<'div'> & { variant?: Variant }

/** Aviso visível. Erro e aviso são anunciados na hora (role="alert"); os demais, com educação (role="status"). */
export function Alert({ variant = 'info', className = '', ...rest }: Props) {
  return (
    <div
      role={variant === 'error' || variant === 'warning' ? 'alert' : 'status'}
      className={`rounded-control p-3 text-sm ring-1 ring-inset ${variants[variant]} ${className}`}
      {...rest}
    />
  )
}
