import type { ComponentProps } from 'react'
import { FieldFrame } from './FieldFrame'

type Props = Omit<ComponentProps<'input'>, 'id'> & {
  label: string
  /** Mensagem de erro do campo (do Zod ou da API via `setError`). */
  error?: string
  hint?: string
}

/** Rótulo + campo + dica + erro, ligados por id/aria. O `ref` e os handlers do `register` do react-hook-form passam direto. */
export function Field({ label, error, hint, className = '', ...input }: Props) {
  return (
    <FieldFrame label={label} error={error} hint={hint}>
      {(control) => <input {...control} className={`${control.className} min-h-11 ${className}`} {...input} />}
    </FieldFrame>
  )
}
