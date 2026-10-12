import { useState } from 'react'
import { useParams } from 'react-router'
import { useVehicle } from '../api/hooks/vehicles'
import { Button } from '../components/Button'
import { ButtonLink } from '../components/ButtonLink'
import { Card } from '../components/Card'
import { Km } from '../components/Km'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { formatInstantDate, toNumber } from '../lib/format'
import { fuelLabels } from '../lib/labels'
import { OdometerDialog } from './vehicle/OdometerDialog'
import { VehicleLoadError } from './vehicle/VehicleLoadError'

// Provisório: a Task F4 troca o corpo desta tela por histórico, lembretes e compartilhamento; o cabeçalho com a km fica.
export function VehicleDetail() {
  const { id = '' } = useParams()
  const vehicle = useVehicle(id)
  const [updatingKm, setUpdatingKm] = useState(false)

  if (vehicle.isPending) {
    return (
      <p className="flex items-center gap-2 py-8 text-slate-600">
        <Spinner size="sm" label={null} /> Carregando veículo…
      </p>
    )
  }
  if (vehicle.error || !vehicle.data) return <VehicleLoadError error={vehicle.error} onRetry={() => void vehicle.refetch()} />

  const current = vehicle.data
  const fullName = `${current.make} ${current.model} ${current.modelYear}`

  return (
    <div>
      <PageHeader
        title={current.nickname ?? fullName}
        back={{ to: '/', label: 'Painel' }}
        actions={
          <ButtonLink to={`/vehicles/${id}/edit`} variant="secondary">
            Editar
          </ButtonLink>
        }
      >
        <p className="mt-1 text-slate-600">
          {[current.nickname && fullName, current.version, fuelLabels[current.fuel]].filter(Boolean).join(' · ')}
        </p>
        {current.plate && (
          <span className="mt-2 inline-block rounded-md border border-slate-300 bg-white px-2 py-0.5 font-mono text-sm font-semibold tracking-wider">
            {current.plate}
          </span>
        )}
      </PageHeader>

      <Card className="flex items-center justify-between gap-3">
        <div>
          <p className="text-sm text-slate-500">Quilometragem</p>
          <Km value={current.currentOdometer} className="block text-2xl font-bold" />
          <p className="text-sm text-slate-500">atualizada em {formatInstantDate(current.odometerUpdatedAt)}</p>
        </div>
        <Button onClick={() => setUpdatingKm(true)}>Atualizar km</Button>
      </Card>

      <p className="mt-6 text-center text-sm text-slate-500">Histórico, lembretes e compartilhamento deste veículo chegam em seguida.</p>

      {updatingKm && (
        <OdometerDialog
          vehicleId={id}
          currentKm={toNumber(current.currentOdometer) ?? 0}
          onClose={() => setUpdatingKm(false)}
        />
      )}
    </div>
  )
}
