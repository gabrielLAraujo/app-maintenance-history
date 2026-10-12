import { useState } from 'react'
import { useDeleteReminder, useVehicleReminders } from '../../api/hooks/reminders'
import type { components } from '../../api/schema'
import { errorMessage } from '../../api/unwrap'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Empty } from '../../components/Empty'
import { Modal } from '../../components/Modal'
import { Spinner } from '../../components/Spinner'
import { formatDate, formatInstantDayMonth, formatKm, toNumber } from '../../lib/format'
import { reminderStateLabels } from '../../lib/labels'
import { ReminderDialog } from './ReminderDialog'
import { intervalSummary, isOdometerStale, statusLines } from './reminderStatus'

type Schemas = components['schemas']
type Reminder = Schemas['ReminderDto']

// O vencido primeiro, depois o próximo, depois o em dia; dentro de cada grupo, a ordem da API.
const severity = { overdue: 0, dueSoon: 1, ok: 2 } satisfies Record<Schemas['ReminderState'], number>

const look = {
  overdue: { bar: 'bg-red-500', badge: 'bg-red-100 text-red-800' },
  dueSoon: { bar: 'bg-amber-500', badge: 'bg-amber-100 text-amber-900' },
  ok: { bar: 'bg-emerald-500', badge: 'bg-emerald-100 text-emerald-800' },
} satisfies Record<Schemas['ReminderState'], { bar: string; badge: string }>

type Props = {
  vehicleId: string
  /** Km atual do veículo: a base sugerida de um lembrete novo. */
  currentKm: number
  /** Quando a km foi atualizada pela última vez; passando de 30 dias o aviso por km perde a confiança. */
  odometerUpdatedAt: string
  /** Abre a janela de atualizar a km (que mora na tela do veículo). */
  onUpdateKm: () => void
}

type Dialog = { kind: 'create' } | { kind: 'edit'; id: string }

/** Aba Lembretes: o estado de cada um (em dia, próximo, vencido) e o que falta, mais criar, editar e excluir. */
export function RemindersTab({ vehicleId, currentKm, odometerUpdatedAt, onUpdateKm }: Props) {
  const reminders = useVehicleReminders(vehicleId)
  const [dialog, setDialog] = useState<Dialog | null>(null)
  const [removing, setRemoving] = useState<Reminder | null>(null)

  if (reminders.isPending) {
    return (
      <p className="flex items-center gap-2 py-8 text-slate-600">
        <Spinner size="sm" label={null} /> Carregando lembretes…
      </p>
    )
  }

  if (reminders.error || !reminders.data) {
    return (
      <div className="space-y-3">
        <Alert variant="error">{errorMessage(reminders.error)}</Alert>
        <Button variant="secondary" onClick={() => void reminders.refetch()}>
          Tentar de novo
        </Button>
      </div>
    )
  }

  const items = [...reminders.data].sort((a, b) => severity[a.status.state] - severity[b.status.state])
  // Só lembrete por km depende da leitura do painel; um que conta por tempo não se importa com a km velha.
  const staleKm = items.some((item) => toNumber(item.intervalKm) !== null) && isOdometerStale(odometerUpdatedAt)
  // A janela de edição segue o lembrete da lista: se ele sumir (excluído em outra tela), a janela some junto.
  const editing = dialog?.kind === 'edit' ? items.find((item) => item.id === dialog.id) : undefined

  return (
    <div className="space-y-4">
      {staleKm && (
        <Alert variant="warning" className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <span>Km atualizada em {formatInstantDayMonth(odometerUpdatedAt)}. Os avisos por km podem estar atrasados.</span>
          <Button variant="secondary" onClick={onUpdateKm}>
            Atualizar km
          </Button>
        </Alert>
      )}

      {items.length === 0 ? (
        <Empty
          title="Nenhum lembrete ainda"
          actions={<Button onClick={() => setDialog({ kind: 'create' })}>Criar o primeiro lembrete</Button>}
        >
          <p>Avise-se da troca de óleo, da correia, do fluido de freio. O lembrete vence pela km, pelo tempo, ou pelo que chegar primeiro.</p>
        </Empty>
      ) : (
        <>
          <Button className="w-full sm:w-auto" onClick={() => setDialog({ kind: 'create' })}>
            Novo lembrete
          </Button>
          <ul className="space-y-3">
            {items.map((reminder) => (
              <ReminderRow
                key={reminder.id}
                reminder={reminder}
                onEdit={() => setDialog({ kind: 'edit', id: reminder.id })}
                onRemove={() => setRemoving(reminder)}
              />
            ))}
          </ul>
        </>
      )}

      {(dialog?.kind === 'create' || editing) && (
        <ReminderDialog vehicleId={vehicleId} currentKm={currentKm} reminder={editing} onClose={() => setDialog(null)} />
      )}
      {removing && <DeleteDialog vehicleId={vehicleId} reminder={removing} onClose={() => setRemoving(null)} />}
    </div>
  )
}

function ReminderRow({ reminder, onEdit, onRemove }: { reminder: Reminder; onEdit: () => void; onRemove: () => void }) {
  const { state } = reminder.status
  const lastKm = toNumber(reminder.lastDoneOdometer)
  const lastDone = `Última vez: ${formatDate(reminder.lastDoneDate)}${lastKm === null ? '' : ` · ${formatKm(lastKm)}`}`

  return (
    <li data-state={state} className="flex gap-3 rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200">
      <span aria-hidden="true" className={`w-1.5 shrink-0 self-stretch rounded-full ${look[state].bar}`} />
      <div className="min-w-0 flex-1">
        <div className="flex items-start justify-between gap-3">
          <h3 className="min-w-0 font-semibold leading-snug break-words">{reminder.title}</h3>
          <span className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-semibold ${look[state].badge}`}>
            {reminderStateLabels[state]}
          </span>
        </div>
        <p className="mt-1 text-sm text-slate-600">{intervalSummary(reminder)}</p>

        <dl className="mt-3 space-y-1">
          {statusLines(reminder.status).map((line) => (
            <div key={line.axis} className="flex items-baseline justify-between gap-3 text-sm">
              <dt className="text-slate-500">{line.label}</dt>
              <dd className={`text-right font-medium tabular-nums ${line.late ? 'text-red-700' : 'text-slate-900'}`}>{line.text}</dd>
            </div>
          ))}
        </dl>
        <p className="mt-2 text-sm text-slate-500">{lastDone}</p>

        <div className="mt-3 grid grid-cols-2 gap-2 sm:flex">
          <Button variant="secondary" aria-label={`Editar ${reminder.title}`} onClick={onEdit}>
            Editar
          </Button>
          <Button variant="secondary" aria-label={`Excluir ${reminder.title}`} onClick={onRemove}>
            Excluir
          </Button>
        </div>
      </div>
    </li>
  )
}

function DeleteDialog({ vehicleId, reminder, onClose }: { vehicleId: string; reminder: Reminder; onClose: () => void }) {
  const remove = useDeleteReminder(vehicleId)

  async function confirm() {
    try {
      await remove.mutateAsync(reminder.id)
      onClose()
    } catch {
      // O erro fica em remove.error, mostrado na janela.
    }
  }

  return (
    <Modal title="Excluir este lembrete?" onClose={onClose}>
      <div className="mt-3 space-y-4">
        <p className="break-words text-slate-700">
          <strong>{reminder.title}</strong> será apagado de vez. Os registros do histórico não mudam.
        </p>
        {remove.error && <Alert variant="error">{errorMessage(remove.error)}</Alert>}
        <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <Button variant="secondary" onClick={onClose}>
            Cancelar
          </Button>
          <Button variant="danger" loading={remove.isPending} onClick={() => void confirm()}>
            Excluir de vez
          </Button>
        </div>
      </div>
    </Modal>
  )
}
