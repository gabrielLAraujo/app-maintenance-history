import { useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useDeleteRecord, useRecord } from '../api/hooks/records'
import { useVehicle } from '../api/hooks/vehicles'
import type { components } from '../api/schema'
import { errorMessage } from '../api/unwrap'
import { Alert } from '../components/Alert'
import { Button } from '../components/Button'
import { ButtonLink } from '../components/ButtonLink'
import { Card } from '../components/Card'
import { Km } from '../components/Km'
import { Modal } from '../components/Modal'
import { Money } from '../components/Money'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { formatDate, formatMoney, formatQuantity, toNumber } from '../lib/format'
import { partUnitLabels } from '../lib/labels'
import { Attachments } from './record/Attachments'
import { CategoryBadge, TypeBadge } from './record/RecordBadges'
import { RecordLoadError } from './record/RecordLoadError'

type Schemas = components['schemas']

/** Um registro por inteiro: dados, peças com subtotal e total, galeria de anexos e a exclusão. */
export function RecordDetail() {
  const { id = '', recordId = '' } = useParams()
  const record = useRecord(id, recordId)
  // Já está no cache quando se vem da tela do veículo; serve só para dar nome ao "voltar".
  const vehicle = useVehicle(id)
  const vehiclePath = `/vehicles/${id}`

  if (record.isPending) {
    return (
      <p className="flex items-center gap-2 py-8 text-slate-600">
        <Spinner size="sm" label={null} /> Carregando registro…
      </p>
    )
  }
  if (record.error || !record.data) {
    return <RecordLoadError error={record.error} onRetry={() => void record.refetch()} backTo={vehiclePath} />
  }

  const current = record.data
  const odometer = toNumber(current.odometer)
  const vehicleName = vehicle.data ? (vehicle.data.nickname ?? `${vehicle.data.make} ${vehicle.data.model}`) : null

  return (
    <div className="space-y-4">
      <PageHeader
        title={current.title}
        back={{ to: vehiclePath, label: vehicleName ?? 'Voltar ao veículo' }}
        actions={
          <ButtonLink to={`${vehiclePath}/records/${recordId}/edit`} variant="secondary">
            Editar
          </ButtonLink>
        }
      >
        <div className="mt-2 flex flex-wrap items-center gap-2">
          <TypeBadge type={current.type} />
          <CategoryBadge category={current.category} />
        </div>
        <p className="mt-2 text-slate-600">
          {formatDate(current.date)} · {odometer === null ? 'sem km informada' : <Km value={odometer} />}
        </p>
      </PageHeader>

      <Details record={current} />
      <Costs record={current} />
      <Attachments vehicleId={id} recordId={recordId} attachments={current.attachments} />
      <DeleteRecord vehicleId={id} recordId={recordId} record={current} />
    </div>
  )
}

function Details({ record }: { record: Schemas['RecordDto'] }) {
  if (!record.description && !record.performedBy && !record.replacedOriginalPart) return null

  return (
    <Card className="space-y-4">
      {record.description && <p className="whitespace-pre-line break-words text-slate-800">{record.description}</p>}
      <dl className="grid gap-4 sm:grid-cols-2">
        {record.performedBy && (
          <div>
            <dt className="text-sm text-slate-500">Quem fez</dt>
            <dd className="font-medium break-words">{record.performedBy}</dd>
          </div>
        )}
        {record.replacedOriginalPart && (
          <div className="sm:col-span-2">
            <dt className="text-sm text-slate-500">Peça original substituída</dt>
            <dd className="font-medium break-words">{record.replacedOriginalPart}</dd>
          </div>
        )}
      </dl>
    </Card>
  )
}

function Costs({ record }: { record: Schemas['RecordDto'] }) {
  const labor = toNumber(record.laborCost)
  if (record.parts.length === 0 && labor === null) return null

  return (
    <Card aria-labelledby="costs-title">
      <h2 id="costs-title" className="text-base font-semibold">
        {record.parts.length > 0 ? 'Peças e custo' : 'Custo'}
      </h2>

      {record.parts.length > 0 && (
        <ul className="mt-1 divide-y divide-slate-100">
          {record.parts.map((part, index) => {
            const unitPrice = toNumber(part.unitPrice)
            const quantity = `${formatQuantity(toNumber(part.quantity) ?? 0)} ${partUnitLabels[part.unit]}`
            const identity = [part.brand, part.partNumber && `Cód. ${part.partNumber}`].filter(Boolean).join(' · ')

            return (
              <li key={index} className="flex items-start justify-between gap-3 py-3">
                <div className="min-w-0">
                  <p className="font-medium break-words">{part.name}</p>
                  {identity && <p className="text-sm text-slate-600 break-words">{identity}</p>}
                  <p className="text-sm text-slate-600 tabular-nums">
                    {unitPrice === null ? quantity : `${quantity} × ${formatMoney(unitPrice)}`}
                  </p>
                </div>
                {unitPrice !== null && <Money value={part.lineTotal} className="shrink-0 font-semibold" />}
              </li>
            )
          })}
        </ul>
      )}

      <dl className="mt-1 space-y-2 border-t border-slate-200 pt-3">
        {labor !== null && (
          <div className="flex items-center justify-between gap-3">
            <dt className="text-slate-600">Mão de obra</dt>
            <dd>
              <Money value={labor} className="font-semibold" />
            </dd>
          </div>
        )}
        <div className="flex items-center justify-between gap-3">
          <dt className="font-medium">Total</dt>
          <dd>
            <Money value={record.totalCost} className="text-xl font-bold" />
          </dd>
        </div>
      </dl>
    </Card>
  )
}

function DeleteRecord({ vehicleId, recordId, record }: { vehicleId: string; recordId: string; record: Schemas['RecordDto'] }) {
  const navigate = useNavigate()
  const remove = useDeleteRecord(vehicleId)
  const [confirming, setConfirming] = useState(false)
  const files = record.attachments.length

  async function confirm() {
    try {
      await remove.mutateAsync(recordId)
      navigate(`/vehicles/${vehicleId}`, { replace: true })
    } catch {
      // O erro fica em remove.error, mostrado na janela.
    }
  }

  return (
    <Card>
      <h2 className="text-base font-semibold">Excluir registro</h2>
      <p className="mt-1 text-sm text-slate-600">
        Apaga o registro, as peças e os anexos. A km do veículo e os lembretes não voltam atrás. Não dá para desfazer.
      </p>
      <Button variant="danger" className="mt-4" onClick={() => setConfirming(true)}>
        Excluir registro
      </Button>

      {confirming && (
        <Modal title="Excluir este registro?" onClose={() => setConfirming(false)}>
          <div className="mt-3 space-y-4">
            <p className="break-words text-slate-700">
              <strong>{record.title}</strong> será apagado de vez
              {files > 0 && `, com ${files === 1 ? 'o anexo' : `os ${files} anexos`}`}.
            </p>
            {remove.error && <Alert variant="error">{errorMessage(remove.error)}</Alert>}
            <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
              <Button variant="secondary" onClick={() => setConfirming(false)}>
                Cancelar
              </Button>
              <Button variant="danger" loading={remove.isPending} onClick={() => void confirm()}>
                Excluir de vez
              </Button>
            </div>
          </div>
        </Modal>
      )}
    </Card>
  )
}
