import { Link } from 'react-router'
import { useAllReminders } from '../api/hooks/reminders'
import type { components } from '../api/schema'
import { errorMessage } from '../api/unwrap'
import { Alert } from '../components/Alert'
import { Badge } from '../components/Badge'
import { Button } from '../components/Button'
import { ButtonLink } from '../components/ButtonLink'
import { Card } from '../components/Card'
import { Empty } from '../components/Empty'
import { PageHeader } from '../components/PageHeader'
import { Skeleton } from '../components/Skeleton'
import { statusSummary } from './vehicle/reminderStatus'

type Reminder = components['schemas']['DueReminderDto']
type State = components['schemas']['ReminderState']

const GROUPS: { state: State; title: string; badge: string }[] = [
  { state: 'overdue', title: 'Vencidos', badge: 'Vencido' },
  { state: 'dueSoon', title: 'Próximos', badge: 'Próximo' },
  { state: 'ok', title: 'Em dia', badge: 'Em dia' },
]

function Group({ title, badge, state, items }: { title: string; badge: string; state: State; items: Reminder[] }) {
  const id = `grupo-${state}`

  return (
    <Card aria-labelledby={id}>
      <div className="flex items-baseline gap-2">
        <h2 id={id} className="text-lg font-bold">
          {title}
        </h2>
        <span className="font-mono text-sm font-semibold text-muted">{items.length}</span>
      </div>
      <ul className="mt-3 divide-y divide-track">
        {items.map((item) => (
          <li key={item.reminderId}>
            <Link
              to={`/vehicles/${item.vehicleId}?tab=reminders`}
              className="-mx-2 flex min-h-14 items-center gap-3 rounded-control px-2 py-2.5 hover:bg-ground focus-visible:outline-2 focus-visible:outline-ink"
            >
              <span className="min-w-0 flex-1">
                <span className="block truncate font-semibold">{item.title}</span>
                <span className="block text-[13px] text-ink-2">
                  {item.vehicleName} · {statusSummary(item.status)}
                </span>
              </span>
              <Badge tone={state} className="shrink-0">
                {badge}
              </Badge>
            </Link>
          </li>
        ))}
      </ul>
    </Card>
  )
}

/** Todos os lembretes de todos os veículos, agrupados em vencidos, próximos e em dia. */
export function Reminders() {
  const reminders = useAllReminders()

  return (
    <div>
      <PageHeader title="Lembretes">
        <p className="mt-1 text-muted">De todos os veículos, por km ou por tempo, o que vier primeiro.</p>
      </PageHeader>

      {reminders.isPending ? (
        <div className="space-y-4" role="status" aria-label="Carregando lembretes">
          <Skeleton className="h-40 rounded-card" />
          <Skeleton className="h-40 rounded-card" />
        </div>
      ) : reminders.error ? (
        <div className="space-y-3">
          <Alert variant="error">{errorMessage(reminders.error)}</Alert>
          <Button variant="secondary" onClick={() => void reminders.refetch()}>
            Tentar de novo
          </Button>
        </div>
      ) : reminders.data.length === 0 ? (
        <Empty title="Nenhum lembrete ainda" actions={<ButtonLink to="/vehicles">Ver veículos</ButtonLink>}>
          <p>Os lembretes ficam na aba Lembretes de cada veículo: troca de óleo a cada 5.000 km, fluido de freio a cada 2 anos.</p>
        </Empty>
      ) : (
        <div className="space-y-4">
          {GROUPS.map((group) => {
            const items = reminders.data.filter((item) => item.status.state === group.state)
            return items.length > 0 && <Group key={group.state} {...group} items={items} />
          })}
        </div>
      )}
    </div>
  )
}
