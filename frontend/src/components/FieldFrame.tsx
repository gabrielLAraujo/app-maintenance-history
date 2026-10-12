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
      <label htmlFor={id} className="block text-sm font-medium text-slate-700">
        {label}
      </label>
      {children({
        id,
        'aria-invalid': error ? true : undefined,
        'aria-describedby': describedBy,
        // 16px (text-base) evita o zoom automático do iOS ao focar o campo.
        className: `block w-full rounded-lg border bg-white px-3 text-base text-slate-900 placeholder:text-slate-400 focus:outline-2 focus:outline-offset-0 disabled:bg-slate-100 disabled:text-slate-500 ${
          error ? 'border-red-500 focus:outline-red-600' : 'border-slate-300 focus:outline-amber-500'
        }`,
      })}
      {hint && (
        <p id={hintId} className="text-sm text-slate-500">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} role="alert" className="text-sm text-red-700">
          {error}
        </p>
      )}
    </div>
  )
}
