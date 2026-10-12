import { Card } from '../components/Card'

// Provisório: a Task F3 troca esta tela pelo painel de veículos e lembretes.
export function Dashboard() {
  return (
    <Card>
      <h1 className="text-xl font-semibold">Seus veículos</h1>
      <p className="mt-2 text-slate-600">O painel de veículos e lembretes chega em seguida.</p>
    </Card>
  )
}
