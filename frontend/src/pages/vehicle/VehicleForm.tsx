import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { applyProblemToForm, type Problem } from '../../api/problem'
import type { components } from '../../api/schema'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { ButtonLink } from '../../components/ButtonLink'
import { Card } from '../../components/Card'
import { Field } from '../../components/Field'
import { SelectField } from '../../components/SelectField'
import { TextAreaField } from '../../components/TextAreaField'
import { fuelLabels, vehicleTypeLabels } from '../../lib/labels'
import { INVALID_KM_MESSAGE, isValidPlate, normalizePlate, parseKm } from '../../lib/parse'

type Schemas = components['schemas']
type VehicleType = Schemas['VehicleType']
type Fuel = Schemas['Fuel']

const VEHICLE_TYPES = ['car', 'motorcycle', 'truck'] as const satisfies readonly VehicleType[]
const FUELS = ['gasoline', 'ethanol', 'flex', 'diesel', 'hybrid', 'electric', 'other'] as const satisfies readonly Fuel[]

const text = (max: number, what: string) => z.string().trim().max(max, `${what} deve ter no máximo ${max} caracteres.`)
const year = z.string().trim().regex(/^\d{4}$/, 'Informe o ano com 4 dígitos.')

const shape = {
  type: z.enum(VEHICLE_TYPES),
  make: text(60, 'A marca').min(1, 'Informe a marca.'),
  model: text(60, 'O modelo').min(1, 'Informe o modelo.'),
  version: text(120, 'A versão'),
  generation: text(30, 'A geração'),
  manufactureYear: year,
  modelYear: year,
  fuel: z.enum(FUELS, 'Escolha o combustível.'),
  nickname: text(60, 'O apelido'),
  plate: z.string().refine((value) => value.trim() === '' || isValidPlate(value), 'Placa inválida.'),
  vin: text(17, 'O chassi'),
  notes: text(2000, 'As observações'),
  currentOdometer: z.string(),
}

const isYear = (value: unknown) => typeof value === 'string' && /^\d{4}$/.test(value)
const yearsOf = (payload: { value: unknown }) => payload.value as { manufactureYear?: string; modelYear?: string }

/**
 * As mesmas regras do back-end. As de ano comparam dois campos e por isso rodam sobre o formulário todo; o `when` as
 * mantém ativas mesmo com outro campo inválido (o combustível em branco, por exemplo), desde que os anos estejam bem formados.
 */
function buildSchema(requireOdometer: boolean) {
  const latest = new Date().getFullYear() + 1

  return z
    .object({
      ...shape,
      currentOdometer: requireOdometer
        ? z.string().refine((value) => parseKm(value) !== null, INVALID_KM_MESSAGE)
        : shape.currentOdometer,
    })
    .refine((values) => Number(values.manufactureYear) >= 1900 && Number(values.manufactureYear) <= latest, {
      path: ['manufactureYear'],
      message: `O ano de fabricação vai de 1900 a ${latest}.`,
      when: (payload) => isYear(yearsOf(payload).manufactureYear),
    })
    .refine(
      (values) => {
        const manufacture = Number(values.manufactureYear)
        return Number(values.modelYear) === manufacture || Number(values.modelYear) === manufacture + 1
      },
      {
        path: ['modelYear'],
        message: 'O ano-modelo é o ano de fabricação ou o seguinte.',
        when: (payload) => isYear(yearsOf(payload).manufactureYear) && isYear(yearsOf(payload).modelYear),
      },
    )
}

const createSchema = buildSchema(true)
const editSchema = buildSchema(false)

/** Valores do formulário: tudo texto, como os campos; `VehicleFormData` é o que sai pronto para a API. */
export type VehicleFormValues = z.infer<typeof createSchema>

export type VehicleFormData = Omit<Schemas['UpdateVehicleRequest'], 'catalog' | 'rowVersion'> & {
  /** Só no cadastro; na edição a km muda pela atualização de km. */
  currentOdometer?: number
}

const DEFAULTS: VehicleFormValues = {
  type: 'car',
  make: '',
  model: '',
  version: '',
  generation: '',
  manufactureYear: '',
  modelYear: '',
  // Sem palpite: combustível errado em silêncio é pior que um campo a mais.
  fuel: '' as Fuel,
  nickname: '',
  plate: '',
  vin: '',
  notes: '',
  currentOdometer: '',
}

const orNull = (value: string) => value.trim() || null

function toData(values: VehicleFormValues, mode: 'create' | 'edit', lockedType: VehicleType | undefined): VehicleFormData {
  return {
    type: lockedType ?? values.type,
    make: values.make.trim(),
    model: values.model.trim(),
    version: orNull(values.version),
    generation: orNull(values.generation),
    manufactureYear: Number(values.manufactureYear),
    modelYear: Number(values.modelYear),
    fuel: values.fuel,
    nickname: orNull(values.nickname),
    plate: orNull(normalizePlate(values.plate)),
    vin: orNull(values.vin),
    notes: orNull(values.notes),
    // Só no cadastro (a edição não manda a km: ela muda pela atualização de km).
    ...(mode === 'create' ? { currentOdometer: parseKm(values.currentOdometer) ?? undefined } : {}),
  }
}

type Props = {
  mode: 'create' | 'edit'
  initial?: Partial<VehicleFormValues>
  /** Veículo ligado à FIPE: o tipo fica travado, porque o vínculo é por tipo. */
  lockType?: boolean
  submitLabel: string
  /** Devolve `null` quando deu certo; senão o Problem (erro de campo vai para o campo, o resto para um aviso). */
  onSubmit: (data: VehicleFormData) => Promise<Problem | null>
  cancelTo?: string
}

/** Formulário do veículo, igual no cadastro e na edição (o cadastro soma a km atual). */
export function VehicleForm({ mode, initial, lockType = false, submitLabel, onSubmit, cancelTo }: Props) {
  const [formError, setFormError] = useState<string | null>(null)
  const lockedType = lockType ? initial?.type : undefined
  // O ano-modelo acompanha o ano de fabricação enquanto começou em branco e o usuário não mexeu nele.
  const [followManufactureYear, setFollowManufactureYear] = useState(!initial?.modelYear)
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<VehicleFormValues>({
    resolver: zodResolver(mode === 'create' ? createSchema : editSchema),
    defaultValues: { ...DEFAULTS, ...initial },
  })

  const submit = handleSubmit(async (values) => {
    setFormError(null)
    const problem = await onSubmit(toData(values, mode, lockedType))
    if (!problem) return

    // Erro de campo vai para o campo; o que não é de nenhum campo do formulário (ex.: catalog.fipeCode) vai para o aviso.
    const general = applyProblemToForm(problem, setError)
    const stray = Object.entries(problem.errors ?? {})
      .filter(([key]) => !Object.hasOwn(shape, key))
      .flatMap(([, messages]) => messages)
    setFormError(general ?? (stray.length > 0 ? stray.join(' ') : null))
  })

  const manufactureYear = register('manufactureYear', {
    onChange: (event) => {
      if (followManufactureYear) setValue('modelYear', event.target.value)
    },
  })
  const modelYear = register('modelYear', { onChange: () => setFollowManufactureYear(false) })

  return (
    <form onSubmit={submit} noValidate className="space-y-4">
      {formError && <Alert variant="error">{formError}</Alert>}

      <Card>
        <h2 className="text-base font-semibold">Dados do veículo</h2>
        <div className="mt-4 grid gap-4 sm:grid-cols-2">
          {lockedType ? (
            <SelectField
              label="Tipo"
              hint="Vinculado à FIPE; não muda."
              disabled
              value={lockedType}
            >
              <option value={lockedType}>{vehicleTypeLabels[lockedType]}</option>
            </SelectField>
          ) : (
            <SelectField label="Tipo" error={errors.type?.message} {...register('type')}>
              {VEHICLE_TYPES.map((value) => (
                <option key={value} value={value}>
                  {vehicleTypeLabels[value]}
                </option>
              ))}
            </SelectField>
          )}
          <SelectField label="Combustível" error={errors.fuel?.message} {...register('fuel')}>
            <option value="">Selecione</option>
            {FUELS.map((value) => (
              <option key={value} value={value}>
                {fuelLabels[value]}
              </option>
            ))}
          </SelectField>
          <Field label="Marca" autoComplete="off" error={errors.make?.message} {...register('make')} />
          <Field label="Modelo" autoComplete="off" error={errors.model?.message} {...register('model')} />
          <Field label="Versão (opcional)" autoComplete="off" error={errors.version?.message} {...register('version')} />
          <Field
            label="Geração (opcional)"
            hint="Ex.: EK. A FIPE não informa."
            autoComplete="off"
            error={errors.generation?.message}
            {...register('generation')}
          />
          <Field
            label="Ano de fabricação"
            inputMode="numeric"
            maxLength={4}
            autoComplete="off"
            error={errors.manufactureYear?.message}
            {...manufactureYear}
          />
          <Field
            label="Ano-modelo"
            inputMode="numeric"
            maxLength={4}
            autoComplete="off"
            error={errors.modelYear?.message}
            {...modelYear}
          />
        </div>
      </Card>

      <Card>
        <h2 className="text-base font-semibold">Identificação e uso</h2>
        <div className="mt-4 grid gap-4 sm:grid-cols-2">
          <Field
            label="Apelido (opcional)"
            hint="Aparece no lugar de marca e modelo."
            autoComplete="off"
            error={errors.nickname?.message}
            {...register('nickname')}
          />
          <Field
            label="Placa (opcional)"
            hint="ABC1D23 ou ABC-1234."
            autoCapitalize="characters"
            autoComplete="off"
            maxLength={8}
            className="uppercase"
            error={errors.plate?.message}
            {...register('plate')}
          />
          {mode === 'create' && (
            <Field
              label="Km atual"
              hint="A do painel hoje."
              inputMode="numeric"
              autoComplete="off"
              error={errors.currentOdometer?.message}
              {...register('currentOdometer')}
            />
          )}
          <Field label="Chassi (opcional)" autoComplete="off" maxLength={17} error={errors.vin?.message} {...register('vin')} />
          <div className="sm:col-span-2">
            <TextAreaField label="Observações (opcional)" error={errors.notes?.message} {...register('notes')} />
          </div>
        </div>
      </Card>

      <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
        {cancelTo && (
          <ButtonLink to={cancelTo} variant="secondary">
            Cancelar
          </ButtonLink>
        )}
        <Button type="submit" loading={isSubmitting}>
          {submitLabel}
        </Button>
      </div>
    </form>
  )
}
