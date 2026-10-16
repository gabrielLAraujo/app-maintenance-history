import type { components } from '../api/schema'
import { formatMoney, toNumber } from '../lib/format'
import { categoryLabels } from '../lib/labels'

type CategoryCost = components['schemas']['CategoryCostDto']

/** Gasto por categoria, do maior para o menor (a ordem que a API manda): rótulo e valor por escrito, barra de desenho. */
export function CategoryBars({ items }: { items: CategoryCost[] }) {
  const values = items.map((item) => ({ category: item.category, cost: toNumber(item.cost) ?? 0 }))
  const max = Math.max(...values.map((item) => item.cost), 0)

  return (
    <ul className="flex flex-col gap-3">
      {values.map((item) => (
        <li key={item.category} className="flex flex-col gap-1">
          <div className="flex justify-between gap-2 text-sm">
            <span>{categoryLabels[item.category]}</span>
            <span className="font-mono font-semibold">{formatMoney(item.cost)}</span>
          </div>
          <div aria-hidden="true" className="h-2 overflow-hidden rounded-full bg-track">
            <div className="h-full rounded-full bg-ink" style={{ width: max > 0 ? `${Math.max(2, (item.cost / max) * 100)}%` : '0%' }} />
          </div>
        </li>
      ))}
    </ul>
  )
}
