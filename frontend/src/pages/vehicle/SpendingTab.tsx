import { useVehicleSummary } from '../../api/hooks/summary'
import { errorMessage } from '../../api/unwrap'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Card } from '../../components/Card'
import { CategoryBars } from '../../components/CategoryBars'
import { MonthBars } from '../../components/MonthBars'
import { Skeleton } from '../../components/Skeleton'
import { toNumber } from '../../lib/format'

/** Aba Gastos: por categoria e mês a mês (os últimos 12 meses, o atual em destaque). */
export function SpendingTab({ vehicleId }: { vehicleId: string }) {
  const summary = useVehicleSummary(vehicleId)

  if (summary.isPending) {
    return (
      <div className="grid gap-4 lg:grid-cols-2" role="status" aria-label="Carregando gastos">
        <Skeleton className="h-56 rounded-card" />
        <Skeleton className="h-56 rounded-card" />
      </div>
    )
  }

  if (summary.error) {
    return (
      <div className="space-y-3">
        <Alert variant="error">{errorMessage(summary.error)}</Alert>
        <Button variant="secondary" onClick={() => void summary.refetch()}>
          Tentar de novo
        </Button>
      </div>
    )
  }

  const { byCategory, byMonth } = summary.data
  if ((toNumber(summary.data.totalCost) ?? 0) === 0 && byCategory.length === 0) {
    return (
      <p className="rounded-card border-2 border-dashed border-line-strong bg-surface px-4 py-8 text-center font-semibold">
        Nenhum gasto registrado ainda
      </p>
    )
  }

  const lastMonth = byMonth[byMonth.length - 1]?.month

  return (
    <div className="grid items-start gap-4 lg:grid-cols-2">
      <Card aria-labelledby="por-categoria">
        <h2 id="por-categoria" className="mb-4 text-lg font-bold">
          Por categoria
        </h2>
        {byCategory.length > 0 ? (
          <CategoryBars items={byCategory} />
        ) : (
          <p className="text-sm text-muted">Os registros ainda não têm custo.</p>
        )}
      </Card>
      <Card aria-labelledby="por-mes">
        <div className="mb-4 flex items-baseline justify-between gap-3">
          <h2 id="por-mes" className="text-lg font-bold">
            Por mês
          </h2>
          <span className="text-[13px] text-muted">últimos 12 meses</span>
        </div>
        <MonthBars months={byMonth} highlight={lastMonth} label="Gastos por mês nos últimos 12 meses" />
      </Card>
    </div>
  )
}
