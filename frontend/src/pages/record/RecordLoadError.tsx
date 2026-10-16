import { errorMessage, problemOf } from '../../api/unwrap'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { ButtonLink } from '../../components/ButtonLink'
import { Card } from '../../components/Card'

type Props = {
  error: unknown
  onRetry: () => void
  /** Para onde o botão "Voltar" leva (o veículo de onde se veio). */
  backTo: string
}

/** Falha ao carregar um registro: 404 (não existe mais, ou não é seu) tem tela própria; o resto, aviso com "Tentar de novo". */
export function RecordLoadError({ error, onRetry, backTo }: Props) {
  if (problemOf(error).status === 404) {
    return (
      <Card className="text-center">
        <h1 className="text-xl font-semibold">Registro não encontrado</h1>
        <p className="mt-2 text-ink-2">Ele pode ter sido excluído, ou o endereço está errado.</p>
        <ButtonLink to={backTo} className="mt-4">
          Voltar ao veículo
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
