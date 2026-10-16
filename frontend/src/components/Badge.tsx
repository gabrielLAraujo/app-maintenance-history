import type { ReactNode } from 'react'

export type BadgeTone = 'overdue' | 'dueSoon' | 'ok' | 'maintenance' | 'modification' | 'neutral'

const tones: Record<BadgeTone, string> = {
  // Estados de lembrete: pílula com borda; diferem também em claridade, e o texto sempre diz o estado.
  overdue: 'rounded-full border border-danger-line bg-danger-soft px-2.5 py-0.5 font-bold text-danger',
  dueSoon: 'rounded-full border border-amber-line bg-amber-soft px-2.5 py-0.5 font-bold text-amber-ink',
  ok: 'rounded-full border border-ok-line bg-ok-soft px-2.5 py-0.5 font-bold text-ok',
  // Tipos de registro e categorias: etiqueta retangular.
  maintenance: 'rounded-chip bg-track px-2 py-0.5 font-semibold text-ink-2',
  modification: 'rounded-chip bg-plate-soft px-2 py-0.5 font-semibold text-plate',
  neutral: 'rounded-chip border border-line px-2 py-0.5 font-medium text-ink-2',
}

type Props = {
  tone: BadgeTone
  /** O texto do estado ou do tipo: a cor nunca vai sozinha. */
  children: ReactNode
  className?: string
}

export function Badge({ tone, children, className = '' }: Props) {
  return (
    <span className={`inline-flex items-center whitespace-nowrap text-xs ${tones[tone]} ${className}`}>{children}</span>
  )
}
