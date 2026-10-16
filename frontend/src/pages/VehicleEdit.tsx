import { useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useDeleteVehicle, useUpdateVehicle, useVehicle } from '../api/hooks/vehicles'
import type { Problem } from '../api/problem'
import { CONFLICT_MESSAGE, errorMessage, isConflict, problemOf } from '../api/unwrap'
import { Alert } from '../components/Alert'
import { Button } from '../components/Button'
import { Card } from '../components/Card'
import { Modal } from '../components/Modal'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { VehicleForm, type VehicleFormData, type VehicleFormValues } from './vehicle/VehicleForm'
import { VehicleLoadError } from './vehicle/VehicleLoadError'

/** Edição: o mesmo formulário do cadastro, sem o assistente da FIPE (o vínculo, se houver, segue como está). */
export function VehicleEdit() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const vehicle = useVehicle(id)
  const update = useUpdateVehicle()
  const [notice, setNotice] = useState<string | null>(null)

  if (vehicle.isPending) {
    return (
      <p className="flex items-center gap-2 py-8 text-ink-2">
        <Spinner size="sm" label={null} /> Carregando veículo…
      </p>
    )
  }
  if (vehicle.error || !vehicle.data) return <VehicleLoadError error={vehicle.error} onRetry={() => void vehicle.refetch()} />

  const current = vehicle.data
  const initial: Partial<VehicleFormValues> = {
    type: current.type,
    make: current.make,
    model: current.model,
    version: current.version ?? '',
    generation: current.generation ?? '',
    manufactureYear: String(current.manufactureYear),
    modelYear: String(current.modelYear),
    fuel: current.fuel,
    nickname: current.nickname ?? '',
    plate: current.plate ?? '',
    vin: current.vin ?? '',
    notes: current.notes ?? '',
  }

  async function submit(data: VehicleFormData): Promise<Problem | null> {
    setNotice(null)

    try {
      await update.mutateAsync({ id, body: { ...data, catalog: current.catalog, rowVersion: current.rowVersion } })
      navigate(`/vehicles/${id}`)
      return null
    } catch (error) {
      const problem = problemOf(error)
      if (!isConflict(problem)) return problem

      // Alguém (outra aba) salvou antes: o hook já buscou o veículo de novo e o formulário recomeça com o que há no servidor.
      setNotice(CONFLICT_MESSAGE)
      return null
    }
  }

  return (
    <div>
      <PageHeader title="Editar veículo" back={{ to: `/vehicles/${id}`, label: 'Voltar ao veículo' }} />

      <div className="space-y-6">
        {notice && <Alert variant="warning">{notice}</Alert>}
        <VehicleForm
          // A versão nova do servidor (depois de um 409) recria o formulário com os valores atuais.
          key={String(current.rowVersion)}
          mode="edit"
          initial={initial}
          lockType={current.catalog !== null}
          submitLabel="Salvar alterações"
          onSubmit={submit}
          cancelTo={`/vehicles/${id}`}
        />
        <DeleteVehicle id={id} name={current.nickname ?? `${current.make} ${current.model} ${current.modelYear}`} />
      </div>
    </div>
  )
}

function DeleteVehicle({ id, name }: { id: string; name: string }) {
  const navigate = useNavigate()
  const remove = useDeleteVehicle()
  const [confirming, setConfirming] = useState(false)

  async function confirm() {
    try {
      await remove.mutateAsync(id)
      navigate('/', { replace: true })
    } catch {
      // O erro fica em remove.error, mostrado na janela.
    }
  }

  return (
    <Card>
      <h2 className="text-base font-semibold">Excluir veículo</h2>
      <p className="mt-1 text-sm text-ink-2">Apaga o veículo com todo o histórico, lembretes e anexos. Não dá para desfazer.</p>
      <Button variant="danger" className="mt-4" onClick={() => setConfirming(true)}>
        Excluir veículo
      </Button>

      {confirming && (
        <Modal title="Excluir este veículo?" onClose={() => setConfirming(false)}>
          <div className="mt-3 space-y-4">
            <p className="text-ink-2">
              <strong>{name}</strong> e tudo que foi registrado nele serão apagados de vez.
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
