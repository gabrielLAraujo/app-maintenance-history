import type { ComponentProps } from 'react'
import { FieldFrame } from './FieldFrame'

type Props = Omit<ComponentProps<'textarea'>, 'id'> & {
  label: string
  error?: string
  hint?: string
}

/** Texto de várias linhas com o mesmo rótulo, dica e erro do `Field`. */
export function TextAreaField({ label, error, hint, className = '', ...textarea }: Props) {
  return (
    <FieldFrame label={label} error={error} hint={hint}>
      {(control) => <textarea rows={3} {...control} className={`${control.className} py-2 ${className}`} {...textarea} />}
    </FieldFrame>
  )
}
