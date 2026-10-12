import { Link } from 'react-router'
import { useSeedDemo } from '../api/hooks/demo'
import { useDueReminders } from '../api/hooks/reminders'
import { useVehicles } from '../api/hooks/vehicles'
import type { components } from '../api/schema'
import { errorMessage, problemOf } from '../api/unwrap'
import { Alert } from '../components/Alert'
import { Button } from '../components/Button'
import { ButtonLink } from '../components/ButtonLink'
import { Card } from '../components/Card'
import { Empty } from '../components/Empty'
import { Km } from '../components/Km'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { formatKm, toNumber } from '../lib/format'

type Schemas = components['schemas']

function Loading({ children }: { children: string }) {
  return (
    <p className="flex items-center gap-2 py-8 text-slate-600">
      <Spinner size="sm" label={null} /> {children}
    </p>
  )
}

/** Painel: o que pede atenção (lembretes vencidos e próximos) e os veículos; sem veículos, o caminho para começar. */
export function Dashboard() {
  const vehicles = useVehicles()
  const hasVehicles = (vehicles.data?.length ?? 0) > 0

  return (
    <div>
      <PageHeader title="Seus veículos" actions={hasVehicles && <ButtonLink to="/vehicles/new">Novo veículo</ButtonLink>} />

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
          <Attention />
          <VehicleCards vehicles={vehicles.data} />
        </div>
      )}
    </div>
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
          <p className="text-sm text-slate-500">ou, para ver o app funcionando antes:</p>
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

function VehicleCards({ vehicles }: { vehicles: Schemas['VehicleSummaryDto'][] }) {
  return (
    <ul className="grid gap-3 sm:grid-cols-2">
      {vehicles.map((vehicle) => {
        const fullName = `${vehicle.make} ${vehicle.model} ${vehicle.modelYear}`

        return (
          <li key={vehicle.id}>
            <Link
              to={`/vehicles/${vehicle.id}`}
              className="block h-full rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200 transition hover:shadow-md hover:ring-slate-300 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber-500"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <h2 className="truncate text-lg font-semibold">{vehicle.displayName}</h2>
                  {vehicle.displayName !== fullName && <p className="truncate text-sm text-slate-500">{fullName}</p>}
                </div>
                {vehicle.plate && (
                  <span className="shrink-0 rounded-md border border-slate-300 bg-slate-50 px-2 py-0.5 font-mono text-sm font-semibold tracking-wider">
                    {vehicle.plate}
                  </span>
                )}
              </div>
              <Km value={vehicle.currentOdometer} className="mt-3 block text-xl font-semibold text-slate-800" />
            </Link>
          </li>
        )
      })}
    </ul>
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
  overdue: { label: 'Vencido', bar: 'bg-red-500', badge: 'bg-red-100 text-red-800' },
  dueSoon: { label: 'Próximo', bar: 'bg-amber-500', badge: 'bg-amber-100 text-amber-900' },
} as const

/** Lembretes vencidos (vermelho) e próximos (âmbar) de todos os veículos, os vencidos primeiro. */
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
      <p className="rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-900 ring-1 ring-inset ring-emerald-200" role="status">
        Tudo em dia: nenhum lembrete vencido ou próximo.
      </p>
    )
  }

  return (
    <Card aria-labelledby="attention-title">
      <h2 id="attention-title" className="text-lg font-semibold">
        Atenção
      </h2>
      <ul className="mt-2 divide-y divide-slate-100">
        {items.map((item) => {
          const look = appearance[item.status.state]

          return (
            <li key={item.reminderId}>
              <Link
                to={`/vehicles/${item.vehicleId}`}
                data-state={item.status.state}
                className="-mx-2 flex min-h-14 items-center gap-3 rounded-lg px-2 py-3 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-amber-500"
              >
                <span aria-hidden="true" className={`w-1.5 self-stretch rounded-full ${look.bar}`} />
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-medium">{item.title}</span>
                  <span className="block text-sm text-slate-600">
                    {item.vehicleName} · {statusText(item.status)}
                  </span>
                </span>
                <span className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-semibold ${look.badge}`}>{look.label}</span>
              </Link>
            </li>
          )
        })}
      </ul>
    </Card>
  )
}
