import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useCreateReminder, useUpdateReminder } from '../../api/hooks/reminders'
import type { Problem } from '../../api/problem'
import type { components } from '../../api/schema'
import { CONFLICT_MESSAGE, isConflict, problemOf } from '../../api/unwrap'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Field } from '../../components/Field'
import { Modal } from '../../components/Modal'
import { todayInSaoPaulo } from '../../lib/format'
import {
  applyReminderProblem,
  emptyReminderValues,
  reminderSchema,
  reminderToValues,
  valuesToPayload,
  type ReminderFormValues,
} from './reminderValues'

type Schemas = components['schemas']

type Props = {
  vehicleId: string
  /** Km atual do veículo: o ponto de partida de um lembrete novo. */
  currentKm: number
  /** Sem lembrete, a janela cria um novo; com ele, edita. */
  reminder?: Schemas['ReminderDto']
  onClose: () => void
}

/** Janela de criar ou editar um lembrete (título, intervalo em km e/ou meses, e a base: data e km da última vez). */
export function ReminderDialog({ vehicleId, currentKm, reminder, onClose }: Props) {
  const create = useCreateReminder(vehicleId)
  const update = useUpdateReminder(vehicleId)
  const [notice, setNotice] = useState<string | null>(null)

  async function submit(payload: Schemas['CreateReminderRequest']): Promise<Problem | null> {
    setNotice(null)

    try {
      if (reminder) await update.mutateAsync({ reminderId: reminder.id, body: { ...payload, rowVersion: reminder.rowVersion } })
      else await create.mutateAsync(payload)
      onClose()
      return null
    } catch (error) {
      const problem = problemOf(error)
      if (!isConflict(problem)) return problem

      // Alguém (outra aba) salvou antes: o hook já buscou a lista de novo e o formulário recomeça com o que há no servidor.
      setNotice(CONFLICT_MESSAGE)
      return null
    }
  }

  return (
    <Modal title={reminder ? 'Editar lembrete' : 'Novo lembrete'} onClose={onClose}>
      {notice && (
        <Alert variant="warning" className="mt-3">
          {notice}
        </Alert>
      )}
      <ReminderFormBody
        // A versão nova do servidor (depois de um 409) recria o formulário com os valores atuais.
        key={reminder ? String(reminder.rowVersion) : 'new'}
        initial={reminder ? reminderToValues(reminder) : emptyReminderValues(currentKm)}
        submitLabel={reminder ? 'Salvar alterações' : 'Criar lembrete'}
        onSubmit={submit}
        onCancel={onClose}
      />
    </Modal>
  )
}

type BodyProps = {
  initial: ReminderFormValues
  submitLabel: string
  /** Devolve `null` quando deu certo; senão o Problem (erro de campo vai para o campo, o resto para um aviso). */
  onSubmit: (payload: Schemas['CreateReminderRequest']) => Promise<Problem | null>
  onCancel: () => void
}

function ReminderFormBody({ initial, submitLabel, onSubmit, onCancel }: BodyProps) {
  const [formError, setFormError] = useState<string | null>(null)
  const alert = useRef<HTMLDivElement>(null)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ReminderFormValues>({ resolver: zodResolver(reminderSchema), defaultValues: initial })

  // No celular o aviso geral fica no topo, longe do botão: traz para a vista.
  useEffect(() => {
    if (formError) alert.current?.scrollIntoView?.({ block: 'center' })
  }, [formError])

  const submit = handleSubmit(async (values) => {
    setFormError(null)
    const problem = await onSubmit(valuesToPayload(values))
    if (problem) setFormError(applyReminderProblem(problem, setError))
  })

  return (
    <form onSubmit={submit} noValidate className="mt-3 space-y-4">
      {formError && (
        <div ref={alert}>
          <Alert variant="error">{formError}</Alert>
        </div>
      )}

      <Field label="Título" autoComplete="off" placeholder="Ex.: Troca de óleo e filtro" error={errors.title?.message} {...register('title')} />

      <fieldset className="space-y-2">
        <legend className="text-sm font-medium text-slate-700">Repetir a cada</legend>
        <p className="text-sm text-slate-500">Preencha km, meses ou os dois: o aviso vale para o que chegar primeiro.</p>
        <div className="grid grid-cols-2 gap-3">
          <Field
            label="Intervalo em km"
            inputMode="numeric"
            autoComplete="off"
            placeholder="10.000"
            error={errors.intervalKm?.message}
            {...register('intervalKm')}
          />
          <Field
            label="Intervalo em meses"
            inputMode="numeric"
            autoComplete="off"
            placeholder="12"
            error={errors.intervalMonths?.message}
            {...register('intervalMonths')}
          />
        </div>
      </fieldset>

      <fieldset className="space-y-2">
        <legend className="text-sm font-medium text-slate-700">Quando foi feito pela última vez</legend>
        <Field
          label="Última vez feito em"
          type="date"
          max={todayInSaoPaulo()}
          hint="Se ainda não foi feito, use hoje: a contagem começa agora."
          error={errors.lastDoneDate?.message}
          {...register('lastDoneDate')}
        />
        <Field
          label="Km na última vez"
          inputMode="numeric"
          autoComplete="off"
          hint="Obrigatória para lembretes por km. Já vem com a km atual do veículo."
          error={errors.lastDoneOdometer?.message}
          {...register('lastDoneOdometer')}
        />
      </fieldset>

      <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
        <Button variant="secondary" onClick={onCancel}>
          Cancelar
        </Button>
        <Button type="submit" loading={isSubmitting}>
          {submitLabel}
        </Button>
      </div>
    </form>
  )
}
