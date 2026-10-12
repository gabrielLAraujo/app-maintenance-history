import type { ComponentProps } from 'react'
import { FieldFrame } from './FieldFrame'

type Props = Omit<ComponentProps<'select'>, 'id'> & {
  label: string
  error?: string
  hint?: string
}

/** Lista de opções (<option> como filhos) com o mesmo rótulo, dica e erro do `Field`. */
export function SelectField({ label, error, hint, className = '', children, ...select }: Props) {
  return (
    <FieldFrame label={label} error={error} hint={hint}>
      {(control) => (
        <select {...control} className={`${control.className} min-h-11 ${className}`} {...select}>
          {children}
        </select>
      )}
    </FieldFrame>
  )
}
