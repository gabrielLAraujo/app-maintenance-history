import { Link } from 'react-router'
import { Card } from '../components/Card'

export function NotFound() {
  return (
    <main className="mx-auto flex min-h-dvh max-w-sm flex-col justify-center px-4 py-8">
      <Card className="text-center">
        <h1 className="text-xl font-semibold">Página não encontrada</h1>
        <p className="mt-2 text-slate-600">O endereço não existe ou foi movido.</p>
        <Link to="/" className="mt-4 inline-block font-medium text-slate-900 underline">
          Voltar ao início
        </Link>
      </Card>
    </main>
  )
}
