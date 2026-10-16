import { Link } from 'react-router'
import { coverUrl } from '../api/hooks/summary'
import type { components } from '../api/schema'
import { formatDate, formatKm, formatMoney, toNumber } from '../lib/format'
import { PlateChip } from './PlateChip'

type Vehicle = components['schemas']['VehicleSummaryDto']

function Figure({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-muted">{label}</dt>
      <dd className="truncate font-mono text-sm font-semibold">{value}</dd>
    </div>
  )
}

/**
 * Card de um veículo: capa (ou o convite para pôr uma, que leva ao histórico, onde as fotos estão), nome, placa, km,
 * gasto total e data do último serviço. O card inteiro é o link para a tela do veículo. O título é h3 (dentro da seção
 * "Veículos" do painel) ou h2 (na página Veículos).
 */
export function VehicleCard({ vehicle, headingLevel = 3 }: { vehicle: Vehicle; headingLevel?: 2 | 3 }) {
  const Heading = headingLevel === 2 ? 'h2' : 'h3'
  const fullName = `${vehicle.make} ${vehicle.model} ${vehicle.modelYear}`

  return (
    <Link
      to={`/vehicles/${vehicle.id}`}
      className="group block h-full overflow-hidden rounded-card border border-line bg-surface transition hover:border-line-strong hover:shadow-[0_8px_24px_rgba(20,22,27,0.08)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ink"
    >
      {vehicle.coverPhotoId ? (
        <img
          src={coverUrl(vehicle.id, vehicle.coverPhotoId)}
          alt={vehicle.displayName}
          loading="lazy"
          className="h-40 w-full bg-track object-cover"
        />
      ) : (
        <div className="flex h-40 flex-col items-center justify-center gap-2 bg-track text-muted">
          <svg
            viewBox="0 0 24 24"
            className="size-7"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.6"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="M4 7h3l2-3h6l2 3h3a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V8a1 1 0 0 1 1-1z" />
            <circle cx="12" cy="13" r="4" />
          </svg>
          <span className="text-sm font-semibold">Adicionar foto de capa</span>
        </div>
      )}
      <div className="p-4">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <Heading className="truncate text-lg font-bold group-hover:text-amber-ink">{vehicle.displayName}</Heading>
            {vehicle.displayName !== fullName && <p className="truncate text-sm text-muted">{fullName}</p>}
          </div>
          {vehicle.plate && <PlateChip plate={vehicle.plate} className="shrink-0" />}
        </div>
        <dl className="mt-3 grid grid-cols-3 gap-2 border-t border-track pt-3">
          <Figure label="Quilometragem" value={formatKm(toNumber(vehicle.currentOdometer))} />
          <Figure label="Gasto total" value={formatMoney(toNumber(vehicle.totalCost))} />
          <Figure label="Último serviço" value={vehicle.lastRecordDate ? formatDate(vehicle.lastRecordDate) : '—'} />
        </dl>
      </div>
    </Link>
  )
}
