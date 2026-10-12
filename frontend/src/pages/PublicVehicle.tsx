import { Card } from '../components/Card'

// Provisório: a Task F5 troca esta tela pela página pública do veículo (`/p/:token`, sem login e sem o Layout).
export function PublicVehicle() {
  return (
    <main className="mx-auto max-w-3xl px-4 py-6">
      <Card>
        <h1 className="text-xl font-semibold">Veículo compartilhado</h1>
        <p className="mt-2 text-slate-600">A página pública chega em seguida.</p>
      </Card>
    </main>
  )
}
