import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useRef, useState } from 'react'
import { FormProvider, useForm, useFormContext, useWatch } from 'react-hook-form'
import { useNavigate, useParams } from 'react-router'
import { useAddRecord, useRecord, useUpdateRecord } from '../api/hooks/records'
import { useVehicleReminders } from '../api/hooks/reminders'
import { useVehicle } from '../api/hooks/vehicles'
import type { Problem } from '../api/problem'
import { CONFLICT_MESSAGE, isConflict, problemOf } from '../api/unwrap'
import { Alert } from '../components/Alert'
import { Button } from '../components/Button'
import { ButtonLink } from '../components/ButtonLink'
import { Card } from '../components/Card'
import { Field } from '../components/Field'
import { Money } from '../components/Money'
import { OptionPill } from '../components/OptionPill'
import { PageHeader } from '../components/PageHeader'
import { SelectField } from '../components/SelectField'
import { Spinner } from '../components/Spinner'
import { TextAreaField } from '../components/TextAreaField'
import { formatKm, todayInSaoPaulo, toNumber } from '../lib/format'
import { categories, categoryLabels, recordTypeLabels, recordTypes, reminderStateLabels } from '../lib/labels'
import { PartsFields } from './record/PartsFields'
import { RecordLoadError } from './record/RecordLoadError'
import {
  applyRecordProblem,
  emptyRecordValues,
  estimateTotal,
  recordSchema,
  recordToValues,
  valuesToPayload,
  type RecordFormValues,
  type RecordPayload,
} from './record/recordValues'
import { VehicleLoadError } from './vehicle/VehicleLoadError'

function Loading() {
  return (
    <p className="flex items-center gap-2 py-8 text-ink-2">
      <Spinner size="sm" label={null} /> Carregando…
    </p>
  )
}

/** Cria (`/vehicles/:id/records/new`) ou edita (`.../records/:recordId/edit`) um registro de manutenção ou modificação. */
export function RecordForm() {
  const { id = '', recordId } = useParams()
  return recordId ? <EditRecord vehicleId={id} recordId={recordId} /> : <NewRecord vehicleId={id} />
}

function NewRecord({ vehicleId }: { vehicleId: string }) {
  const navigate = useNavigate()
  const vehicle = useVehicle(vehicleId)
  const add = useAddRecord(vehicleId)

  if (vehicle.isPending) return <Loading />
  if (vehicle.error || !vehicle.data) return <VehicleLoadError error={vehicle.error} onRetry={() => void vehicle.refetch()} />

  async function submit(payload: RecordPayload, fulfilledReminderIds: string[]): Promise<Problem | null> {
    try {
      const { id } = await add.mutateAsync({ ...payload, fulfilledReminderIds })
      // `replace`: o "voltar" depois de salvar não leva ao formulário já enviado. O detalhe é onde se anexa foto e nota.
      navigate(`/vehicles/${vehicleId}/records/${id}`, { replace: true })
      return null
    } catch (error) {
      return problemOf(error)
    }
  }

  return (
    <div>
      <PageHeader title="Novo registro" back={{ to: `/vehicles/${vehicleId}`, label: 'Voltar ao veículo' }} />
      <RecordFormBody
        mode="create"
        vehicleId={vehicleId}
        currentKm={toNumber(vehicle.data.currentOdometer)}
        initial={emptyRecordValues()}
        submitLabel="Salvar registro"
        cancelTo={`/vehicles/${vehicleId}`}
        onSubmit={submit}
      />
    </div>
  )
}

function EditRecord({ vehicleId, recordId }: { vehicleId: string; recordId: string }) {
  const navigate = useNavigate()
  const vehicle = useVehicle(vehicleId)
  const record = useRecord(vehicleId, recordId)
  const update = useUpdateRecord(vehicleId)
  const [notice, setNotice] = useState<string | null>(null)
  const detailPath = `/vehicles/${vehicleId}/records/${recordId}`

  if (vehicle.isPending || record.isPending) return <Loading />
  if (vehicle.error || !vehicle.data) return <VehicleLoadError error={vehicle.error} onRetry={() => void vehicle.refetch()} />
  if (record.error || !record.data) {
    return <RecordLoadError error={record.error} onRetry={() => void record.refetch()} backTo={`/vehicles/${vehicleId}`} />
  }

  const current = record.data

  async function submit(payload: RecordPayload): Promise<Problem | null> {
    setNotice(null)

    try {
      await update.mutateAsync({ recordId, body: { ...payload, rowVersion: current.rowVersion } })
      navigate(detailPath)
      return null
    } catch (error) {
      const problem = problemOf(error)
      if (!isConflict(problem)) return problem

      // Alguém (outra aba) salvou antes: o hook já buscou o registro de novo e o formulário recomeça com o que há no servidor.
      setNotice(CONFLICT_MESSAGE)
      return null
    }
  }

  return (
    <div>
      <PageHeader title="Editar registro" back={{ to: detailPath, label: 'Voltar ao registro' }} />
      <div className="space-y-4">
        {notice && <Alert variant="warning">{notice}</Alert>}
        <RecordFormBody
          // A versão nova do servidor (depois de um 409) recria o formulário com os valores atuais.
          key={String(current.rowVersion)}
          mode="edit"
          vehicleId={vehicleId}
          currentKm={toNumber(vehicle.data.currentOdometer)}
          initial={recordToValues(current)}
          submitLabel="Salvar alterações"
          cancelTo={detailPath}
          onSubmit={submit}
        />
      </div>
    </div>
  )
}

type BodyProps = {
  mode: 'create' | 'edit'
  vehicleId: string
  /** Km atual do veículo, para o atalho "Usar a km atual". */
  currentKm: number | null
  initial: RecordFormValues
  submitLabel: string
  cancelTo: string
  /** Devolve `null` quando deu certo; senão o Problem (erro de campo vai para o campo, o resto para um aviso). */
  onSubmit: (payload: RecordPayload, fulfilledReminderIds: string[]) => Promise<Problem | null>
}

function RecordFormBody({ mode, vehicleId, currentKm, initial, submitLabel, cancelTo, onSubmit }: BodyProps) {
  const [formError, setFormError] = useState<string | null>(null)
  const [fulfilled, setFulfilled] = useState<string[]>([])
  const alert = useRef<HTMLDivElement>(null)
  const form = useForm<RecordFormValues>({ resolver: zodResolver(recordSchema), defaultValues: initial })
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    control,
    formState: { errors, isSubmitting },
  } = form
  const type = useWatch({ control, name: 'type' })

  // No celular o aviso geral fica no topo, longe do botão: traz para a vista.
  useEffect(() => {
    if (formError) alert.current?.scrollIntoView?.({ block: 'center' })
  }, [formError])

  const submit = handleSubmit(async (values) => {
    setFormError(null)
    const problem = await onSubmit(valuesToPayload(values), fulfilled)
    if (problem) setFormError(applyRecordProblem(problem, setError))
  })

  return (
    <FormProvider {...form}>
      <form onSubmit={submit} noValidate className="space-y-4">
        {formError && (
          <Alert ref={alert} variant="error">
            {formError}
          </Alert>
        )}

        <Card>
          <h2 className="text-base font-semibold">O que foi feito</h2>
          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            <fieldset className="sm:col-span-2">
              <legend className="mb-1 text-sm font-medium text-ink-2">Tipo</legend>
              <div className="grid grid-cols-2 gap-2">
                {recordTypes.map((value) => (
                  <OptionPill key={value} label={recordTypeLabels[value]}>
                    <input type="radio" value={value} className="sr-only" {...register('type')} />
                  </OptionPill>
                ))}
              </div>
            </fieldset>

            <div className="sm:col-span-2">
              <Field label="Título" autoComplete="off" error={errors.title?.message} {...register('title')} />
            </div>
            <SelectField label="Categoria" error={errors.category?.message} {...register('category')}>
              <option value="">Selecione</option>
              {categories.map((value) => (
                <option key={value} value={value}>
                  {categoryLabels[value]}
                </option>
              ))}
            </SelectField>
            <Field
              label="Data"
              type="date"
              max={todayInSaoPaulo()}
              error={errors.date?.message}
              {...register('date')}
            />
            <div>
              <Field
                label="Km (opcional)"
                inputMode="numeric"
                autoComplete="off"
                error={errors.odometer?.message}
                {...register('odometer')}
              />
              {currentKm !== null && (
                <button
                  type="button"
                  onClick={() => setValue('odometer', String(currentKm), { shouldDirty: true, shouldValidate: true })}
                  className="mt-1 min-h-11 text-sm font-medium text-ink-2 underline hover:text-ink"
                >
                  Usar a km atual ({formatKm(currentKm)})
                </button>
              )}
            </div>
            <Field
              label="Quem fez (opcional)"
              hint="Você, ou o nome da oficina."
              autoComplete="off"
              error={errors.performedBy?.message}
              {...register('performedBy')}
            />
            <div className="sm:col-span-2">
              <TextAreaField label="Descrição (opcional)" error={errors.description?.message} {...register('description')} />
            </div>
            {type === 'modification' && (
              <div className="sm:col-span-2">
                <Field
                  label="Peça original substituída (opcional)"
                  hint="Ex.: Amortecedores originais guardados."
                  autoComplete="off"
                  error={errors.replacedOriginalPart?.message}
                  {...register('replacedOriginalPart')}
                />
              </div>
            )}
          </div>
        </Card>

        <PartsFields />

        <Card>
          <h2 className="text-base font-semibold">Custo</h2>
          <div className="mt-4 space-y-4">
            <Field
              label="Mão de obra (opcional)"
              hint="Em reais, ex.: 150,00."
              inputMode="decimal"
              autoComplete="off"
              placeholder="0,00"
              error={errors.laborCost?.message}
              {...register('laborCost')}
            />
            <CostTotal />
          </div>
        </Card>

        {mode === 'create' && (
          <FulfillReminders
            vehicleId={vehicleId}
            selected={fulfilled}
            onToggle={(id, checked) =>
              setFulfilled((ids) => (checked ? [...ids, id] : ids.filter((other) => other !== id)))
            }
          />
        )}

        <div className="sticky bottom-0 z-10 -mx-4 flex gap-3 border-t border-line bg-white/95 px-4 py-3 backdrop-blur">
          <ButtonLink to={cancelTo} variant="secondary">
            Cancelar
          </ButtonLink>
          <Button type="submit" loading={isSubmitting} className="flex-1">
            {submitLabel}
          </Button>
        </div>
      </form>
    </FormProvider>
  )
}

/** Total do registro com o que está digitado agora: peças × preço + mão de obra. */
function CostTotal() {
  const { control } = useFormContext<RecordFormValues>()
  const laborCost = useWatch({ control, name: 'laborCost' })
  const parts = useWatch({ control, name: 'parts' })
  const total = estimateTotal(laborCost, parts)

  return (
    <p className="flex items-center justify-between gap-3 border-t border-line pt-3">
      <span className="font-medium text-ink-2">Total do registro</span>
      <Money value={total} className="text-xl font-bold" />
    </p>
  )
}

type FulfillProps = {
  vehicleId: string
  selected: string[]
  onToggle: (reminderId: string, checked: boolean) => void
}

/** "Cumpre lembretes": um checkbox por lembrete do veículo; os marcados voltam a contar a partir deste registro. */
function FulfillReminders({ vehicleId, selected, onToggle }: FulfillProps) {
  const reminders = useVehicleReminders(vehicleId)

  if (reminders.isPending) return null

  if (reminders.error) {
    return (
      <Alert variant="warning" className="flex items-center justify-between gap-3">
        <span>Não foi possível carregar os lembretes do veículo.</span>
        <Button variant="secondary" onClick={() => void reminders.refetch()}>
          Tentar de novo
        </Button>
      </Alert>
    )
  }

  if (reminders.data.length === 0) return null

  return (
    <Card>
      <fieldset>
        <legend className="text-base font-semibold">Este registro cumpre lembretes?</legend>
        <p className="mt-1 text-sm text-ink-2">
          Os marcados voltam a contar a partir da data e da km deste registro. Lembretes por km pedem a km preenchida.
        </p>
        <ul className="mt-3 divide-y divide-track">
          {reminders.data.map((reminder) => (
            <li key={reminder.id}>
              <label className="flex min-h-12 cursor-pointer items-center gap-3 py-2">
                <input
                  type="checkbox"
                  className="size-5 shrink-0 accent-amber"
                  checked={selected.includes(reminder.id)}
                  onChange={(event) => onToggle(reminder.id, event.target.checked)}
                />
                <span className="min-w-0 flex-1">{reminder.title}</span>
                <span className="shrink-0 text-xs font-semibold text-muted">{reminderStateLabels[reminder.status.state]}</span>
              </label>
            </li>
          ))}
        </ul>
      </fieldset>
    </Card>
  )
}
