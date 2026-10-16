import { useFieldArray, useFormContext, useWatch } from 'react-hook-form'
import { Button } from '../../components/Button'
import { Card } from '../../components/Card'
import { Field } from '../../components/Field'
import { Money } from '../../components/Money'
import { SelectField } from '../../components/SelectField'
import { partUnitNames, partUnits } from '../../lib/labels'
import { emptyPart, lineTotal, MAX_PARTS, type RecordFormValues } from './recordValues'

/**
 * Peças do registro em linhas dinâmicas: cada peça é um bloco (nome em linha cheia, o resto em duas colunas, que
 * cabem em 360px) com o subtotal ao vivo. Precisa estar dentro de um `FormProvider`.
 */
export function PartsFields() {
  const {
    control,
    register,
    formState: { errors },
  } = useFormContext<RecordFormValues>()
  const { fields, append, remove } = useFieldArray({ control, name: 'parts' })
  const parts = useWatch({ control, name: 'parts' })

  return (
    <Card>
      <h2 className="text-base font-semibold">Peças</h2>
      <p className="mt-1 text-sm text-ink-2">Opcional. Com os preços, o registro soma o custo.</p>

      {fields.length > 0 && (
        <ol className="mt-4 space-y-4">
          {fields.map((field, index) => {
            const own = errors.parts?.[index]

            return (
              <li key={field.id}>
                <fieldset className="rounded-lg border border-line bg-ground/60 p-3">
                  <legend className="px-1 text-sm font-semibold text-ink-2">Peça {index + 1}</legend>
                  <div className="grid grid-cols-2 gap-3">
                    <div className="col-span-2">
                      <Field
                        label="Nome da peça"
                        autoComplete="off"
                        error={own?.name?.message}
                        {...register(`parts.${index}.name`)}
                      />
                    </div>
                    <Field
                      label="Marca (opcional)"
                      autoComplete="off"
                      error={own?.brand?.message}
                      {...register(`parts.${index}.brand`)}
                    />
                    <Field
                      label="Part number (opcional)"
                      autoComplete="off"
                      error={own?.partNumber?.message}
                      {...register(`parts.${index}.partNumber`)}
                    />
                    <Field
                      label="Quantidade"
                      inputMode="decimal"
                      autoComplete="off"
                      error={own?.quantity?.message}
                      {...register(`parts.${index}.quantity`)}
                    />
                    <SelectField label="Unidade" error={own?.unit?.message} {...register(`parts.${index}.unit`)}>
                      {partUnits.map((unit) => (
                        <option key={unit} value={unit}>
                          {partUnitNames[unit]}
                        </option>
                      ))}
                    </SelectField>
                    <div className="col-span-2">
                      <Field
                        label="Preço unitário (opcional)"
                        inputMode="decimal"
                        autoComplete="off"
                        placeholder="0,00"
                        error={own?.unitPrice?.message}
                        {...register(`parts.${index}.unitPrice`)}
                      />
                    </div>
                  </div>
                  <div className="mt-3 flex items-center justify-between gap-3">
                    <Button variant="secondary" aria-label={`Remover peça ${index + 1}`} onClick={() => remove(index)}>
                      Remover
                    </Button>
                    {parts?.[index] && lineTotal(parts[index]) > 0 && (
                      <p className="text-sm text-ink-2">
                        Subtotal <Money value={lineTotal(parts[index])} className="font-semibold text-ink" />
                      </p>
                    )}
                  </div>
                </fieldset>
              </li>
            )
          })}
        </ol>
      )}

      <Button
        variant="secondary"
        className="mt-4 w-full sm:w-auto"
        disabled={fields.length >= MAX_PARTS}
        onClick={() => append(emptyPart())}
      >
        Adicionar peça
      </Button>
      {errors.parts?.message && (
        <p role="alert" className="mt-2 text-sm text-danger">
          {errors.parts.message}
        </p>
      )}
    </Card>
  )
}
