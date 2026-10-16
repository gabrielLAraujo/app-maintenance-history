import type { ReactNode } from 'react'

type Tone = 'default' | 'danger' | 'amber' | 'ok'

const tones: Record<Tone, string> = {
  default: 'text-ink',
  danger: 'text-danger',
  amber: 'text-amber-ink',
  ok: 'text-ok',
}

type Props = {
  label: string
  /** O número, já formatado (km, R$, quantidade). */
  value: ReactNode
  hint?: ReactNode
  tone?: Tone
  className?: string
}

/** Indicador: rótulo, valor grande em mono e uma linha de contexto. */
export function Kpi({ label, value, hint, tone = 'default', className = '' }: Props) {
  return (
    <div className={`rounded-card border border-line bg-surface px-4 py-4 sm:px-5 ${className}`}>
      <p className="text-[13px] font-semibold text-muted">{label}</p>
      <p className={`mt-1 font-mono text-xl leading-tight font-semibold tracking-[-0.02em] sm:text-[28px] ${tones[tone]}`}>{value}</p>
      {hint && <p className="mt-0.5 text-[13px] text-muted">{hint}</p>}
    </div>
  )
}
