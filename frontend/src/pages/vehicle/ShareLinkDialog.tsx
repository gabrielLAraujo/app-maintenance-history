import { zodResolver } from '@hookform/resolvers/zod'
import type { ComponentProps } from 'react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { useCreateShareLink } from '../../api/hooks/sharing'
import { applyProblemToForm, type Problem } from '../../api/problem'
import { problemOf } from '../../api/unwrap'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Field } from '../../components/Field'
import { Modal } from '../../components/Modal'
import { endOfDayInSaoPaulo, todayInSaoPaulo } from '../../lib/format'

/** Tamanho máximo do rótulo no back-end. */
const MAX_LABEL = 60

const schema = z.object({
  label: z.string().trim().max(MAX_LABEL, `O rótulo deve ter no máximo ${MAX_LABEL} caracteres.`),
  includeCosts: z.boolean(),
  includePlate: z.boolean(),
  // O dia em que o link deixa de valer (no fim dele). Vazio = sem validade.
  expiresOn: z
    .string()
    .refine(
      (value) => value === '' || (/^\d{4}-\d{2}-\d{2}$/.test(value) && value >= todayInSaoPaulo()),
      'A validade deve ser hoje ou uma data futura.',
    ),
})

type Values = z.infer<typeof schema>

const defaults: Values = { label: '', includeCosts: false, includePlate: false, expiresOn: '' }

// A API conhece a validade como `expiresAt`; no formulário o campo é o dia (`expiresOn`).
const toFormProblem = (problem: Problem): Problem =>
  problem.errors
    ? { ...problem, errors: Object.fromEntries(Object.entries(problem.errors).map(([key, messages]) => [key === 'expiresAt' ? 'expiresOn' : key, messages])) }
    : problem

function CheckOption({ title, description, ...input }: ComponentProps<'input'> & { title: string; description: string }) {
  return (
    <label className="flex min-h-11 cursor-pointer items-start gap-3 py-1">
      <input type="checkbox" className="mt-1 size-5 shrink-0 accent-amber-500" {...input} />
      <span>
        <span className="block font-medium text-slate-900">{title}</span>
        <span className="block text-sm text-slate-500">{description}</span>
      </span>
    </label>
  )
}

type Props = {
  vehicleId: string
  onClose: () => void
  /** O link foi criado (a lista já busca de novo): a tela avisa o que fazer em seguida. */
  onCreated: () => void
}

/** Janela de criar um link público: rótulo, o que mostrar (custos, placa) e validade opcional. Tudo começa desligado. */
export function ShareLinkDialog({ vehicleId, onClose, onCreated }: Props) {
  const create = useCreateShareLink(vehicleId)
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: defaults })

  const submit = handleSubmit(async (values) => {
    setFormError(null)

    try {
      await create.mutateAsync({
        label: values.label.trim() || null,
        includeCosts: values.includeCosts,
        includePlate: values.includePlate,
        // Instante ISO com deslocamento (fim do dia em Brasília): a API normaliza para UTC e exige que seja futuro.
        expiresAt: values.expiresOn ? endOfDayInSaoPaulo(values.expiresOn) : null,
      })
      onCreated()
      onClose()
    } catch (error) {
      setFormError(applyProblemToForm(toFormProblem(problemOf(error)), setError))
    }
  })

  return (
    <Modal title="Novo link público" onClose={onClose}>
      <form onSubmit={submit} noValidate className="mt-3 space-y-4">
        {formError && <Alert variant="error">{formError}</Alert>}

        <Field
          label="Rótulo (opcional)"
          autoComplete="off"
          placeholder="Ex.: Comprador"
          hint="Só você vê. Ajuda a saber para quem foi cada link."
          error={errors.label?.message}
          {...register('label')}
        />

        <fieldset className="space-y-1">
          <legend className="text-sm font-medium text-slate-700">O que mostrar além do histórico</legend>
          <CheckOption
            title="Mostrar custos"
            description="Preço das peças, mão de obra e total de cada registro."
            {...register('includeCosts')}
          />
          <CheckOption title="Mostrar placa" description="A placa do veículo no topo da página." {...register('includePlate')} />
        </fieldset>

        <Field
          label="Validade (opcional)"
          type="date"
          min={todayInSaoPaulo()}
          hint="O link deixa de abrir no fim deste dia. Sem data, vale até você revogar."
          error={errors.expiresOn?.message}
          {...register('expiresOn')}
        />

        <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <Button variant="secondary" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" loading={isSubmitting}>
            Criar link
          </Button>
        </div>
      </form>
    </Modal>
  )
}
