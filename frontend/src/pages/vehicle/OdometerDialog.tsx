import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { useCorrectOdometer, useUpdateOdometer } from '../../api/hooks/vehicles'
import { applyProblemToForm } from '../../api/problem'
import { CONFLICT_MESSAGE, isConflict, problemOf } from '../../api/unwrap'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Field } from '../../components/Field'
import { Km } from '../../components/Km'
import { Modal } from '../../components/Modal'
import { INVALID_KM_MESSAGE, parseKm } from '../../lib/parse'

const schema = z.object({
  km: z.string().refine((text) => parseKm(text) !== null, INVALID_KM_MESSAGE),
})

type Values = z.infer<typeof schema>

type Props = {
  vehicleId: string
  currentKm: number
  onClose: () => void
}

/**
 * Atualizar a km do veículo (só aumenta) e, a partir dela, corrigir um erro de digitação (pode diminuir, mas não abaixo
 * da maior km do histórico). A API recusa o que não vale com 422 e a mensagem pronta aparece no formulário.
 */
export function OdometerDialog({ vehicleId, currentKm, onClose }: Props) {
  const [correcting, setCorrecting] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const update = useUpdateOdometer()
  const correct = useCorrectOdometer()
  const {
    register,
    handleSubmit,
    setError,
    clearErrors,
    formState: { errors, isSubmitting },
  } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { km: String(currentKm) } })

  const submit = handleSubmit(async ({ km }) => {
    const value = parseKm(km)
    if (value === null) return

    setFormError(null)
    try {
      await (correcting ? correct : update).mutateAsync({ id: vehicleId, km: value })
      onClose()
    } catch (error) {
      const problem = problemOf(error)
      setFormError(isConflict(problem) ? CONFLICT_MESSAGE : applyProblemToForm(problem, setError))
    }
  })

  function switchMode(next: boolean) {
    setCorrecting(next)
    setFormError(null)
    clearErrors()
  }

  return (
    <Modal title={correcting ? 'Corrigir km' : 'Atualizar km'} onClose={onClose}>
      <form onSubmit={submit} noValidate className="mt-3 space-y-4">
        <p className="text-sm text-slate-600">
          Km registrada hoje: <Km value={currentKm} className="font-medium text-slate-900" />
        </p>
        {formError && <Alert variant="error">{formError}</Alert>}
        <Field
          label={correcting ? 'Km correta' : 'Km do painel'}
          hint={
            correcting
              ? 'Use só para consertar um erro de digitação. Não pode ficar abaixo da maior km do histórico.'
              : 'Pode só aumentar. Os lembretes por km são recalculados com este valor.'
          }
          inputMode="numeric"
          autoComplete="off"
          error={errors.km?.message}
          {...register('km')}
        />
        <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <Button variant="secondary" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" loading={isSubmitting}>
            {correcting ? 'Corrigir km' : 'Salvar km'}
          </Button>
        </div>
        <button
          type="button"
          onClick={() => switchMode(!correcting)}
          className="min-h-11 w-full text-sm font-medium text-slate-700 underline hover:text-slate-900"
        >
          {correcting ? 'Voltar para atualizar a km' : 'Digitei a km errada antes? Corrigir km'}
        </button>
      </form>
    </Modal>
  )
}
