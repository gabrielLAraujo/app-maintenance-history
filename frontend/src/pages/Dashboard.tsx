import { useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { useSeedDemo } from '../api/hooks/demo'
import { useDueReminders } from '../api/hooks/reminders'
import { useGarageSummary } from '../api/hooks/summary'
import { useVehicles } from '../api/hooks/vehicles'
import type { components } from '../api/schema'
import { errorMessage, problemOf } from '../api/unwrap'
import { Alert } from '../components/Alert'
import { Badge } from '../components/Badge'
import { Button } from '../components/Button'
import { ButtonLink } from '../components/ButtonLink'
import { Card } from '../components/Card'
import { Empty } from '../components/Empty'
import { Kpi } from '../components/Kpi'
import { MonthBars } from '../components/MonthBars'
import { PageHeader } from '../components/PageHeader'
import { Skeleton } from '../components/Skeleton'
import { Spinner } from '../components/Spinner'
import { VehicleCard } from '../components/VehicleCard'
import { formatDate, formatKm, formatMoney, todayInSaoPaulo, toNumber } from '../lib/format'

type Schemas = components['schemas']

function Loading({ children }: { children: string }) {
  return (
    <p className="flex items-center gap-2 py-8 text-muted">
      <Spinner size="sm" label={null} /> {children}
    </p>
  )
}

const MONTHS_SHORT = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez']

/** "2024-11-11" → "nov/2024". */
const monthYear = (iso: string) => `${MONTHS_SHORT[Number(iso.slice(5, 7)) - 1] ?? ''}/${iso.slice(0, 4)}`

/**
 * Painel: o ano em números (gasto, lembretes, registros), o que pede atenção, os veículos em cards com capa, os gastos
 * por mês e os últimos serviços. Sem veículos, o caminho para começar.
 */
export function Dashboard() {
  const vehicles = useVehicles()
  const count = vehicles.data?.length ?? 0
  const hasVehicles = count > 0

  return (
    <div>
      <PageHeader title="Sua garagem" actions={hasVehicles && <ButtonLink to="/vehicles/new">Novo veículo</ButtonLink>}>
        {hasVehicles && <p className="mt-1 text-muted">{plural(count, 'veículo', 'veículos')}</p>}
      </PageHeader>

      {vehicles.isPending ? (
        <Loading>Carregando veículos…</Loading>
      ) : vehicles.error ? (
        <div className="space-y-3">
          <Alert variant="error">{errorMessage(vehicles.error)}</Alert>
          <Button variant="secondary" onClick={() => void vehicles.refetch()}>
            Tentar de novo
          </Button>
        </div>
      ) : !hasVehicles ? (
        <EmptyGarage />
      ) : (
        <div className="space-y-6">
          <YearNumbers />
          <div className="grid items-start gap-6 lg:grid-cols-3">
            <div className="space-y-6 lg:col-span-2">
              <Attention />
              <section aria-labelledby="vehicles-title">
                <h2 id="vehicles-title" className="mb-3 text-lg font-bold">
                  Veículos
                </h2>
                <ul className="grid gap-4 sm:grid-cols-2">
                  {vehicles.data.map((vehicle) => (
                    <li key={vehicle.id}>
                      <VehicleCard vehicle={vehicle} />
                    </li>
                  ))}
                </ul>
              </section>
            </div>
            <div className="space-y-6">
              <YearChart />
              <LatestServices />
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

/** Os quatro números do topo: gasto no ano, vencidos, próximos e registros. */
function YearNumbers() {
  const summary = useGarageSummary()
  const due = useDueReminders()

  const overdue = due.data?.filter((item) => item.status.state === 'overdue') ?? []
  const soon = due.data?.filter((item) => item.status.state === 'dueSoon') ?? []
  const first = (items: Reminder[], none: string) => (items[0] ? `${items[0].title} · ${items[0].vehicleName}` : none)
  const reminderValue = (items: Reminder[]) => (due.data ? String(items.length) : '—')

  return (
    <div className="grid grid-cols-2 gap-3 sm:gap-4 xl:grid-cols-4">
      {summary.data ? (
        <Kpi
          label={`Gasto em ${summary.data.year}`}
          value={formatMoney(toNumber(summary.data.yearCost))}
          hint={`${plural(toNumber(summary.data.yearRecordCount) ?? 0, 'serviço', 'serviços')} em ${plural(toNumber(summary.data.yearVehicleCount) ?? 0, 'veículo', 'veículos')}`}
        />
      ) : summary.error ? (
        <Kpi label="Gasto no ano" value="—" hint="Não foi possível carregar." />
      ) : (
        <Skeleton className="h-[106px] rounded-card" />
      )}
      <Kpi label="Vencidos" value={reminderValue(overdue)} tone={overdue.length > 0 ? 'danger' : 'default'} hint={first(overdue, 'nada vencido')} />
      <Kpi label="Vencem em breve" value={reminderValue(soon)} tone={soon.length > 0 ? 'amber' : 'default'} hint={first(soon, 'nada por perto')} />
      {summary.data ? (
        <Kpi
          label="Registros"
          value={String(toNumber(summary.data.recordCount) ?? 0)}
          hint={summary.data.firstRecordDate ? `desde ${monthYear(summary.data.firstRecordDate)}` : 'nenhum ainda'}
        />
      ) : summary.error ? (
        <Kpi label="Registros" value="—" />
      ) : (
        <Skeleton className="h-[106px] rounded-card" />
      )}
    </div>
  )
}

function SideCard({ id, title, aside, children }: { id: string; title: string; aside?: ReactNode; children: ReactNode }) {
  return (
    <Card aria-labelledby={id}>
      <div className="flex items-baseline justify-between gap-3">
        <h2 id={id} className="text-lg font-bold">
          {title}
        </h2>
        {aside}
      </div>
      <div className="mt-4">{children}</div>
    </Card>
  )
}

/** Gastos de janeiro a dezembro do ano corrente, com o mês atual em destaque. */
function YearChart() {
  const summary = useGarageSummary()
  // O mês de hoje no fuso da API, lido uma vez: é ele que fica em destaque.
  const [current] = useState(() => todayInSaoPaulo().slice(0, 7))
  if (!summary.data) return summary.error ? null : <Skeleton className="h-64 rounded-card" />

  const { year, byMonth } = summary.data

  return (
    <SideCard id="year-chart-title" title={`Gastos em ${year}`} aside={<span className="text-[13px] text-muted">por mês</span>}>
      <MonthBars months={byMonth} highlight={current} label={`Gastos por mês em ${year}`} />
    </SideCard>
  )
}

/** Os quatro serviços mais recentes de todos os veículos. */
function LatestServices() {
  const summary = useGarageSummary()
  if (!summary.data || summary.data.recentRecords.length === 0) return null

  return (
    <SideCard id="latest-title" title="Últimos serviços">
      <ul className="divide-y divide-track">
        {summary.data.recentRecords.map((record) => (
          <li key={record.id}>
            <Link
              to={`/vehicles/${record.vehicleId}/records/${record.id}`}
              className="-mx-2 flex items-center gap-3 rounded-control px-2 py-2.5 hover:bg-ground focus-visible:outline-2 focus-visible:outline-ink"
            >
              <span className="min-w-0 flex-1">
                <span className="block truncate font-semibold">{record.title}</span>
                <span className="block text-[13px] text-muted">
                  {record.vehicleName} · {formatDate(record.date)}
                </span>
              </span>
              <span className="font-mono text-sm font-semibold">{formatMoney(toNumber(record.totalCost))}</span>
            </Link>
          </li>
        ))}
      </ul>
    </SideCard>
  )
}

function EmptyGarage() {
  const seed = useSeedDemo()
  // Com Demo:Enabled desligado a rota nem existe (404).
  const demoOff = seed.error && problemOf(seed.error).status === 404

  return (
    <Empty
      title="Sua garagem está vazia"
      actions={
        <>
          <ButtonLink to="/vehicles/new">Cadastrar veículo</ButtonLink>
          <p className="text-sm text-muted">ou, para ver o app funcionando antes:</p>
          <Button variant="secondary" loading={seed.isPending} onClick={() => seed.mutate()}>
            Carregar dados de exemplo
          </Button>
        </>
      }
    >
      <p>
        Cadastre seu primeiro veículo, ou carregue dois de exemplo (um Civic 1998 e um Gol 2019) com histórico,
        lembretes e link público.
      </p>
      {seed.error && (
        <Alert variant="error" className="mt-4 text-left">
          {demoOff ? 'Os dados de exemplo não estão ligados neste ambiente.' : errorMessage(seed.error)}
        </Alert>
      )}
    </Empty>
  )
}

type Reminder = Schemas['DueReminderDto']

const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`

// Cada eixo do lembrete (km, tempo) vale por si: positivo falta, zero vence agora, negativo já passou.
function axisText(remaining: number | null, ahead: (n: number) => string, now: string, late: (n: number) => string) {
  if (remaining === null) return null
  return remaining > 0 ? ahead(remaining) : remaining === 0 ? now : late(-remaining)
}

function statusText({ kmRemaining, daysRemaining }: Schemas['ReminderStatusDto']): string {
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

const severity = { overdue: 0, dueSoon: 1, ok: 2 } satisfies Record<Schemas['ReminderState'], number>

const appearance = {
  overdue: { label: 'Vencido', row: 'border-danger-line bg-danger-soft', icon: 'text-danger' },
  dueSoon: { label: 'Próximo', row: 'border-amber-line bg-amber-soft', icon: 'text-amber-ink' },
} as const

/** Lembretes vencidos e próximos de todos os veículos, os vencidos primeiro; o estado vai sempre por escrito. */
function Attention() {
  const due = useDueReminders()

  if (due.isPending) return <Loading>Carregando lembretes…</Loading>

  if (due.error) {
    return (
      <Alert variant="warning" className="flex items-center justify-between gap-3">
        <span>Não foi possível carregar os lembretes.</span>
        <Button variant="secondary" onClick={() => void due.refetch()}>
          Tentar de novo
        </Button>
      </Alert>
    )
  }

  const items = due.data
    .filter((item): item is Reminder & { status: { state: 'overdue' | 'dueSoon' } } => item.status.state !== 'ok')
    .sort((a, b) => severity[a.status.state] - severity[b.status.state])

  if (items.length === 0) {
    return (
      <p className="rounded-control bg-ok-soft px-4 py-3 text-sm text-ok ring-1 ring-ok-line ring-inset" role="status">
        Tudo em dia: nenhum lembrete vencido ou próximo.
      </p>
    )
  }

  return (
    <Card aria-labelledby="attention-title">
      <div className="flex items-center justify-between gap-3">
        <h2 id="attention-title" className="text-lg font-bold">
          Pede atenção
        </h2>
        <Link to="/reminders" className="text-sm font-semibold underline-offset-2 hover:text-amber-ink hover:underline">
          Ver lembretes
        </Link>
      </div>
      <ul className="mt-3 flex flex-col gap-2">
        {items.map((item) => {
          const look = appearance[item.status.state]

          return (
            <li key={item.reminderId}>
              <Link
                to={`/vehicles/${item.vehicleId}`}
                data-state={item.status.state}
                className={`flex min-h-14 items-center gap-3.5 rounded-xl border px-3.5 py-2.5 focus-visible:outline-2 focus-visible:outline-ink ${look.row}`}
              >
                <span className={`inline-flex size-9 flex-none items-center justify-center rounded-control bg-surface ${look.icon}`}>
                  <svg
                    viewBox="0 0 24 24"
                    className="size-[18px]"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    aria-hidden="true"
                  >
                    {item.status.state === 'overdue' ? (
                      <>
                        <circle cx="12" cy="12" r="9" />
                        <path d="M12 7v6" />
                        <path d="M12 16.5h.01" />
                      </>
                    ) : (
                      <>
                        <circle cx="12" cy="12" r="9" />
                        <path d="M12 7v5l3 2" />
                      </>
                    )}
                  </svg>
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-semibold">{item.title}</span>
                  <span className="block text-[13px] text-ink-2">
                    {item.vehicleName} · {statusText(item.status)}
                  </span>
                </span>
                <Badge tone={item.status.state} className="shrink-0 bg-surface">
                  {look.label}
                </Badge>
              </Link>
            </li>
          )
        })}
      </ul>
    </Card>
  )
}
