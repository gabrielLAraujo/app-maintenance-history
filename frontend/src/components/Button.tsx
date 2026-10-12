import type { ComponentProps } from 'react'
import { buttonClass, type ButtonVariant } from './buttonStyles'
import { Spinner } from './Spinner'

type Props = ComponentProps<'button'> & {
  variant?: ButtonVariant
  /** Requisição em andamento: mostra o spinner e desabilita o botão (nada de envio em dobro). */
  loading?: boolean
}

export function Button({
  variant = 'primary',
  loading = false,
  disabled,
  type = 'button',
  className = '',
  children,
  ...rest
}: Props) {
  return (
    <button
      type={type}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={buttonClass(variant, className)}
      {...rest}
    >
      {loading && <Spinner size="sm" label={null} />}
      {children}
    </button>
  )
}
