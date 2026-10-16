import { Link } from 'react-router'
import { useAuth } from '../auth/AuthProvider'
import { CredentialsForm } from '../auth/CredentialsForm'
import { Card } from '../components/Card'
import { DemoButton } from './Landing'

export function Login() {
  const { login, registrationEnabled, demoEnabled } = useAuth()

  return (
    <div className="space-y-4">
      <Card>
        <h1 className="text-xl font-bold">Entrar</h1>
        <CredentialsForm submitLabel="Entrar" passwordAutoComplete="current-password" onSubmit={login} />
        {registrationEnabled && (
          <p className="mt-4 text-center text-sm text-ink-2">
            Ainda não tem conta?{' '}
            <Link to="/register" className="font-semibold text-ink underline">
              Criar conta
            </Link>
          </p>
        )}
      </Card>
      {demoEnabled && (
        <Card className="text-center">
          <p className="text-sm text-ink-2">Só quer conhecer? A demo abre já logada, com dois carros de exemplo.</p>
          <DemoButton className="mt-3" />
        </Card>
      )}
    </div>
  )
}
