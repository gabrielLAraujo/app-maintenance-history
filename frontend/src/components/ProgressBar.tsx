export type ProgressTone = 'overdue' | 'dueSoon' | 'ok' | 'neutral'

const fills: Record<ProgressTone, string> = {
  overdue: 'bg-danger',
  dueSoon: 'bg-amber',
  ok: 'bg-ok',
  neutral: 'bg-ink-2',
}

type Props = {
  /** De 0 a 1; fora disso é limitado. */
  value: number
  tone: ProgressTone
  /** Nome da barra para leitores de tela (o lembrete, por exemplo). */
  label: string
  className?: string
}

/** Barra de progresso fina (8 px). O estado vem em texto ao lado: a cor da barra não basta. */
export function ProgressBar({ value, tone, label, className = '' }: Props) {
  const percent = Math.round(Math.min(1, Math.max(0, value)) * 100)

  return (
    <div
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={percent}
      className={`h-2 overflow-hidden rounded-full bg-track ${className}`}
    >
      <div className={`h-full rounded-full ${fills[tone]}`} style={{ width: `${percent}%` }} />
    </div>
  )
}
