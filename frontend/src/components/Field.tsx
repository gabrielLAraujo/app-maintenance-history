import { useId, type ComponentProps } from 'react'

type Props = Omit<ComponentProps<'input'>, 'id'> & {
  label: string
  /** Mensagem de erro do campo (do Zod ou da API via `setError`). */
  error?: string
  hint?: string
}

/** Rótulo + campo + dica + erro, ligados por id/aria. O `ref` e os handlers do `register` do react-hook-form passam direto. */
export function Field({ label, error, hint, className = '', ...input }: Props) {
  const id = useId()
  const hintId = `${id}-hint`
  const errorId = `${id}-error`
  const describedBy = [hint && hintId, error && errorId].filter(Boolean).join(' ') || undefined

  return (
    <div className="space-y-1">
      <label htmlFor={id} className="block text-sm font-medium text-slate-700">
        {label}
      </label>
      <input
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        // 16px (text-base) evita o zoom automático do iOS ao focar o campo.
        className={`block min-h-11 w-full rounded-lg border bg-white px-3 text-base text-slate-900 placeholder:text-slate-400 focus:outline-2 focus:outline-offset-0 disabled:bg-slate-100 ${
          error ? 'border-red-500 focus:outline-red-600' : 'border-slate-300 focus:outline-amber-500'
        } ${className}`}
        {...input}
      />
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
