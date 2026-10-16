import { useId, type ReactNode } from 'react'

type Control = {
  id: string
  'aria-invalid'?: true
  'aria-describedby'?: string
  /** Aparência comum de campo (borda, foco, erro); a altura fica por conta de cada controle. */
  className: string
}

type Props = {
  label: string
  /** Mensagem de erro do campo (do Zod ou da API via `setError`). */
  error?: string
  hint?: string
  children: (control: Control) => ReactNode
}

/** Rótulo + controle + dica + erro, ligados por id/aria. Quem desenha o controle (input, select, textarea) recebe o que ligar a ele. */
export function FieldFrame({ label, error, hint, children }: Props) {
  const id = useId()
  const hintId = `${id}-hint`
  const errorId = `${id}-error`
  const describedBy = [hint && hintId, error && errorId].filter(Boolean).join(' ') || undefined

  return (
    <div className="space-y-1">
      <label htmlFor={id} className="block text-sm font-semibold text-ink">
        {label}
      </label>
      {children({
        id,
        'aria-invalid': error ? true : undefined,
        'aria-describedby': describedBy,
        // 16px (text-base) evita o zoom automático do iOS ao focar o campo.
        className: `block w-full rounded-control border bg-surface px-3 text-base text-ink placeholder:text-muted focus:outline-3 focus:outline-offset-0 disabled:bg-ground disabled:text-muted ${
          error ? 'border-danger focus:outline-danger-line' : 'border-line-strong focus:outline-focus'
        }`,
      })}
      {hint && (
        <p id={hintId} className="text-[13px] text-muted">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} role="alert" className="text-sm text-danger">
          {error}
        </p>
      )}
    </div>
  )
}
