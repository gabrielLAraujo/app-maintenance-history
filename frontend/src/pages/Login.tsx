import { Link } from 'react-router'
import { useAuth } from '../auth/AuthProvider'
import { CredentialsForm } from '../auth/CredentialsForm'
import { Card } from '../components/Card'

export function Login() {
  const { login, registrationEnabled } = useAuth()

  return (
    <Card>
      <h1 className="text-xl font-semibold">Entrar</h1>
      <CredentialsForm submitLabel="Entrar" passwordAutoComplete="current-password" onSubmit={login} />
      {registrationEnabled && (
        <p className="mt-4 text-center text-sm text-slate-600">
          Ainda não tem conta?{' '}
          <Link to="/register" className="font-medium text-slate-900 underline">
            Criar conta
          </Link>
        </p>
      )}
    </Card>
  )
}
