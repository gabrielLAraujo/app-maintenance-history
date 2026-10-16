import { Link, Navigate } from 'react-router'
import { useAuth } from '../auth/AuthProvider'
import { CredentialsForm } from '../auth/CredentialsForm'
import { Card } from '../components/Card'

export function Register() {
  const { register, registrationEnabled } = useAuth()

  // Cadastro fechado: a API nem tem a rota (404). Quem digitou /register na mão vai para o login.
  if (!registrationEnabled) return <Navigate to="/login" replace />

  return (
    <Card>
      <h1 className="text-xl font-semibold">Criar conta</h1>
      <CredentialsForm submitLabel="Criar conta" passwordAutoComplete="new-password" onSubmit={register} />
      <p className="mt-4 text-center text-sm text-ink-2">
        Já tem conta?{' '}
        <Link to="/login" className="font-medium text-ink underline">
          Entrar
        </Link>
      </p>
    </Card>
  )
}
