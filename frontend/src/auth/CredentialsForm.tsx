import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { applyProblemToForm, type Problem } from '../api/problem'
import { Alert } from '../components/Alert'
import { Button } from '../components/Button'
import { Field } from '../components/Field'

// A API exige 10 caracteres de senha e nada além disso; o e-mail é aparado (o teclado do celular costuma deixar espaço).
const schema = z.object({
  email: z.string().trim().pipe(z.email('Informe um e-mail válido.')),
  password: z.string().min(10, 'A senha deve ter pelo menos 10 caracteres.'),
})

type Values = z.infer<typeof schema>

type Props = {
  submitLabel: string
  passwordAutoComplete: 'current-password' | 'new-password'
  /** Devolve `null` quando deu certo; senão o Problem (erro de campo vai para o campo, o resto para um aviso). */
  onSubmit: (email: string, password: string) => Promise<Problem | null>
}

/** Formulário de e-mail e senha do login e do cadastro. */
export function CredentialsForm({ submitLabel, passwordAutoComplete, onSubmit }: Props) {
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Values>({ resolver: zodResolver(schema) })

  const submit = handleSubmit(async ({ email, password }) => {
    setFormError(null)
    const problem = await onSubmit(email, password)
    if (problem) setFormError(applyProblemToForm(problem, setError))
  })

  return (
    <form onSubmit={submit} noValidate className="mt-4 space-y-4">
      {formError && <Alert variant="error">{formError}</Alert>}
      <Field
        label="E-mail"
        type="email"
        autoComplete="email"
        inputMode="email"
        autoCapitalize="none"
        error={errors.email?.message}
        {...register('email')}
      />
      <Field
        label="Senha"
        type="password"
        autoComplete={passwordAutoComplete}
        error={errors.password?.message}
        {...register('password')}
      />
      <Button type="submit" loading={isSubmitting} className="w-full">
        {submitLabel}
      </Button>
    </form>
  )
}
