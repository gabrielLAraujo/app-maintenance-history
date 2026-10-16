import { errorMessage, problemOf } from '../../api/unwrap'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { ButtonLink } from '../../components/ButtonLink'
import { Card } from '../../components/Card'

/** Falha ao carregar um veículo: 404 (não existe ou não é seu) tem tela própria; o resto, aviso com "Tentar de novo". */
export function VehicleLoadError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  if (problemOf(error).status === 404) {
    return (
      <Card className="text-center">
        <h1 className="text-xl font-semibold">Veículo não encontrado</h1>
        <p className="mt-2 text-ink-2">Ele pode ter sido excluído, ou o endereço está errado.</p>
        <ButtonLink to="/" className="mt-4">
          Voltar ao painel
        </ButtonLink>
      </Card>
    )
  }

  return (
    <div className="space-y-3">
      <Alert variant="error">{errorMessage(error)}</Alert>
      <Button variant="secondary" onClick={onRetry}>
        Tentar de novo
      </Button>
    </div>
  )
}
