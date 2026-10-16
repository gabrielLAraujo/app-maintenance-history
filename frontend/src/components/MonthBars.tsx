import type { components } from '../api/schema'
import { formatMoney, toNumber } from '../lib/format'

type MonthCost = components['schemas']['MonthCostDto']

const MONTH_NAMES = [
  'janeiro',
  'fevereiro',
  'março',
  'abril',
  'maio',
  'junho',
  'julho',
  'agosto',
  'setembro',
  'outubro',
  'novembro',
  'dezembro',
]

/** "2026-07" → índice do mês (6). */
const monthIndex = (month: string) => Number(month.slice(5, 7)) - 1

const monthName = (month: string) => MONTH_NAMES[monthIndex(month)] ?? month

/** O que o gráfico mostra, em texto: cada mês com gasto e, se houver, que os demais não tiveram. */
function describe(label: string, months: { month: string; cost: number }[]): string {
  const spent = months.filter((item) => item.cost > 0)
  if (spent.length === 0) return `${label}: nenhum gasto.`

  const list = spent.map((item) => `${monthName(item.month)}: ${formatMoney(item.cost)}`).join(', ')
  return spent.length < months.length ? `${label}: ${list}; demais meses sem gastos.` : `${label}: ${list}.`
}

type Props = {
  months: MonthCost[]
  /** Mês em destaque (`yyyy-MM`), normalmente o atual. */
  highlight?: string
  /** Começo do texto para leitores de tela ("Gastos por mês em 2026"). */
  label: string
  className?: string
}

/** Barras de gasto por mês. As barras são desenho; o `aria-label` da figura diz os valores. */
export function MonthBars({ months, highlight, label, className = '' }: Props) {
  const values = months.map((item) => ({ month: item.month, cost: toNumber(item.cost) ?? 0 }))
  const max = Math.max(...values.map((item) => item.cost), 0)
  const top = values.reduce<{ month: string; cost: number } | null>(
    (best, item) => (item.cost > 0 && (!best || item.cost > best.cost) ? item : best),
    null,
  )

  return (
    <figure aria-label={describe(label, values)} className={className}>
      <div aria-hidden="true" className="flex h-36 items-end gap-1.5">
        {values.map((item) => (
          <div key={item.month} className="flex h-full flex-1 items-end rounded-t-md bg-track/60">
            <div
              className={`w-full rounded-t-md ${item.month === highlight ? 'bg-amber' : 'bg-ink-2'}`}
              style={{ height: max > 0 && item.cost > 0 ? `${Math.max(4, (item.cost / max) * 100)}%` : '0%' }}
            />
          </div>
        ))}
      </div>
      <div aria-hidden="true" className="mt-1.5 flex gap-1.5 font-mono text-[11px] text-muted">
        {values.map((item) => (
          <span key={item.month} className={`flex-1 text-center ${item.month === highlight ? 'font-semibold text-ink' : ''}`}>
            {monthName(item.month).charAt(0).toUpperCase()}
          </span>
        ))}
      </div>
      {top && (
        <figcaption className="mt-3 text-[13px] text-ink-2">
          Maior mês: {monthName(top.month)}, <span className="font-mono font-semibold">{formatMoney(top.cost)}</span>.
          {highlight && ' Em destaque, o mês atual.'}
        </figcaption>
      )}
    </figure>
  )
}
