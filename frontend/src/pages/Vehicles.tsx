import { useVehicles } from '../api/hooks/vehicles'
import { errorMessage } from '../api/unwrap'
import { Alert } from '../components/Alert'
import { Button } from '../components/Button'
import { ButtonLink } from '../components/ButtonLink'
import { Empty } from '../components/Empty'
import { PageHeader } from '../components/PageHeader'
import { Skeleton } from '../components/Skeleton'
import { VehicleCard } from '../components/VehicleCard'

/** Todos os veículos do usuário em cards com capa, em ordem de nome. */
export function Vehicles() {
  const vehicles = useVehicles()
  const count = vehicles.data?.length ?? 0

  return (
    <div>
      <PageHeader title="Veículos" actions={count > 0 && <ButtonLink to="/vehicles/new">Novo veículo</ButtonLink>}>
        {count > 0 && <p className="mt-1 text-muted">{count === 1 ? '1 veículo' : `${count} veículos`}</p>}
      </PageHeader>

      {vehicles.isPending ? (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3" role="status" aria-label="Carregando veículos">
          <Skeleton className="h-72 rounded-card" />
          <Skeleton className="h-72 rounded-card" />
        </div>
      ) : vehicles.error ? (
        <div className="space-y-3">
          <Alert variant="error">{errorMessage(vehicles.error)}</Alert>
          <Button variant="secondary" onClick={() => void vehicles.refetch()}>
            Tentar de novo
          </Button>
        </div>
      ) : count === 0 ? (
        <Empty title="Nenhum veículo cadastrado" actions={<ButtonLink to="/vehicles/new">Cadastrar veículo</ButtonLink>}>
          <p>Cadastre um veículo para começar o histórico de manutenção e modificações.</p>
        </Empty>
      ) : (
        <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {vehicles.data.map((vehicle) => (
            <li key={vehicle.id}>
              <VehicleCard vehicle={vehicle} headingLevel={2} />
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
