import type { components } from '../../api/schema'
import { formatKm, toNumber } from '../../lib/format'

type Schemas = components['schemas']

/** Depois de quantos dias sem atualizar a km o aviso por km deixa de ser confiável (brief: "mais de 30 dias"). */
export const STALE_ODOMETER_DAYS = 30

const DAY_MS = 24 * 60 * 60 * 1000

/** A km do veículo foi atualizada há mais de 30 dias? Data inválida não avisa. `now` existe para os testes. */
export const isOdometerStale = (odometerUpdatedAt: string, now: Date = new Date()): boolean =>
  now.getTime() - new Date(odometerUpdatedAt).getTime() > STALE_ODOMETER_DAYS * DAY_MS

const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`

/** Cada linha de "o que falta" (por km, por tempo). `late` é o eixo que já passou (≤ 0 vence, regra M2 do back-end). */
export type StatusLine = { axis: 'km' | 'time'; label: string; text: string; late: boolean }

/** Positivo falta, zero vence agora, negativo já passou. Eixo ausente (lembrete só por km ou só por tempo) não vira linha. */
export function statusLines({ kmRemaining, daysRemaining }: Schemas['ReminderStatusDto']): StatusLine[] {
  const lines: StatusLine[] = []
  const km = toNumber(kmRemaining)
  const days = toNumber(daysRemaining)

  if (km !== null) {
    const text =
      km > 0 ? `${km === 1 ? 'Falta' : 'Faltam'} ${formatKm(km)}`
      : km === 0 ? 'Vence agora'
      : `Passou ${formatKm(-km)}`
    lines.push({ axis: 'km', label: 'Por km', text, late: km <= 0 })
  }

  if (days !== null) {
    const text =
      days > 0 ? `${days === 1 ? 'Falta' : 'Faltam'} ${plural(days, 'dia', 'dias')}`
      : days === 0 ? 'Vence hoje'
      : `Venceu há ${plural(-days, 'dia', 'dias')}`
    lines.push({ axis: 'time', label: 'Por tempo', text, late: days <= 0 })
  }

  return lines
}

/** "A cada 10.000 km ou 12 meses": o lembrete vence no que vier primeiro. */
export function intervalSummary({
  intervalKm,
  intervalMonths,
}: Pick<Schemas['ReminderDto'], 'intervalKm' | 'intervalMonths'>): string {
  const km = toNumber(intervalKm)
  const months = toNumber(intervalMonths)
  const parts = [km !== null && formatKm(km), months !== null && plural(months, 'mês', 'meses')].filter(Boolean)

  return parts.length > 0 ? `A cada ${parts.join(' ou ')}` : ''
}

// Cada eixo do lembrete (km, tempo) vale por si: positivo falta, zero vence agora, negativo já passou.
function axisText(remaining: number | null, ahead: (n: number) => string, now: string, late: (n: number) => string) {
  if (remaining === null) return null
  return remaining > 0 ? ahead(remaining) : remaining === 0 ? now : late(-remaining)
}

/** A situação numa linha curta, para listas ("faltam 800 km · faltam 12 dias", "300 km atrasado"). */
export function statusSummary({ kmRemaining, daysRemaining }: Schemas['ReminderStatusDto']): string {
  const km = axisText(
    toNumber(kmRemaining),
    (n) => `faltam ${formatKm(n)}`,
    'vence agora',
    (n) => `${formatKm(n)} atrasado`,
  )
  const days = axisText(
    toNumber(daysRemaining),
    (n) => `faltam ${plural(n, 'dia', 'dias')}`,
    'vence hoje',
    (n) => `${plural(n, 'dia', 'dias')} atrasado`,
  )

  return [km, days].filter(Boolean).join(' · ')
}
