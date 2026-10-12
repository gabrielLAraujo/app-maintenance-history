import type { Path, UseFormSetError } from 'react-hook-form'
import { z } from 'zod'
import { problemMessage, toFieldPath, type Problem } from '../../api/problem'
import type { components } from '../../api/schema'
import { formatKmRange, formatMoneyInput, formatQuantity, todayInSaoPaulo, toNumber } from '../../lib/format'
import {
  INVALID_KM_MESSAGE,
  INVALID_MONEY_MESSAGE,
  INVALID_QUANTITY_MESSAGE,
  parseKm,
  parseMoney,
  parseQuantity,
} from '../../lib/parse'

type Schemas = components['schemas']

const RECORD_TYPES = ['maintenance', 'modification'] as const satisfies readonly Schemas['RecordType'][]
const CATEGORIES = [
  'engine',
  'transmission',
  'suspension',
  'brakes',
  'electrical',
  'wheelsTires',
  'bodyPaint',
  'interior',
  'other',
] as const satisfies readonly Schemas['Category'][]
const PART_UNITS = ['unit', 'liter', 'kilogram', 'meter'] as const satisfies readonly Schemas['PartUnit'][]

/** Peças por registro que o back-end aceita. */
export const MAX_PARTS = 50

const text = (max: number, what: string) => z.string().trim().max(max, `${what} deve ter no máximo ${max} caracteres.`)
const blank = (value: string) => value.trim() === ''
const optionalMoney = z.string().refine((value) => blank(value) || parseMoney(value) !== null, INVALID_MONEY_MESSAGE)

const partSchema = z.object({
  name: text(120, 'O nome da peça').min(1, 'Informe o nome da peça.'),
  brand: text(60, 'A marca'),
  partNumber: text(60, 'O part number'),
  quantity: z.string().refine((value) => parseQuantity(value) !== null, INVALID_QUANTITY_MESSAGE),
  unit: z.enum(PART_UNITS),
  unitPrice: optionalMoney,
})

/** As mesmas regras de formato do back-end; as de negócio (data, ordem da km) ele confere e responde 422. */
export const recordSchema = z.object({
  type: z.enum(RECORD_TYPES),
  date: z
    .string()
    .regex(/^\d{4}-\d{2}-\d{2}$/, 'Informe a data.')
    .refine((value) => value <= todayInSaoPaulo(), 'A data não pode estar no futuro.'),
  odometer: z.string().refine((value) => blank(value) || parseKm(value) !== null, INVALID_KM_MESSAGE),
  title: text(120, 'O título').min(1, 'Informe o título.'),
  description: text(4000, 'A descrição'),
  category: z.enum(CATEGORIES, 'Escolha a categoria.'),
  performedBy: text(120, 'Quem fez o serviço'),
  laborCost: optionalMoney,
  replacedOriginalPart: text(500, 'A peça original substituída'),
  parts: z.array(partSchema).max(MAX_PARTS, `Informe no máximo ${MAX_PARTS} peças.`),
})

/** Valores do formulário: tudo texto, como os campos; `RecordPayload` é o que sai pronto para a API. */
export type RecordFormValues = z.infer<typeof recordSchema>
export type PartFormValues = RecordFormValues['parts'][number]

/** Os campos do registro que a criação e a edição têm em comum. */
export type RecordPayload = Omit<Schemas['AddRecordRequest'], 'fulfilledReminderIds'>

export const emptyPart = (): PartFormValues => ({
  name: '',
  brand: '',
  partNumber: '',
  quantity: '1',
  unit: 'unit',
  unitPrice: '',
})

export const emptyRecordValues = (): RecordFormValues => ({
  type: 'maintenance',
  date: todayInSaoPaulo(),
  odometer: '',
  title: '',
  description: '',
  // Sem palpite: categoria errada em silêncio é pior que um toque a mais.
  category: '' as Schemas['Category'],
  performedBy: '',
  laborCost: '',
  replacedOriginalPart: '',
  parts: [],
})

const moneyText = (value: number | string | null | undefined) => {
  const amount = toNumber(value)
  return amount === null ? '' : formatMoneyInput(amount)
}

/** O registro salvo → valores dos campos (valores em reais e quantidades escritos como o usuário digitaria). */
export function recordToValues(record: Schemas['RecordDto']): RecordFormValues {
  const odometer = toNumber(record.odometer)

  return {
    type: record.type,
    date: record.date,
    odometer: odometer === null ? '' : String(odometer),
    title: record.title,
    description: record.description ?? '',
    category: record.category,
    performedBy: record.performedBy ?? '',
    laborCost: moneyText(record.laborCost),
    replacedOriginalPart: record.replacedOriginalPart ?? '',
    parts: record.parts.map((part) => ({
      name: part.name,
      brand: part.brand ?? '',
      partNumber: part.partNumber ?? '',
      quantity: formatQuantity(toNumber(part.quantity) ?? 1),
      unit: part.unit,
      unitPrice: moneyText(part.unitPrice),
    })),
  }
}

const orNull = (value: string) => value.trim() || null

/** Valores já validados → corpo da API: números de verdade, vazio vira `null`, e peça original só em modificação. */
export function valuesToPayload(values: RecordFormValues): RecordPayload {
  return {
    type: values.type,
    date: values.date,
    odometer: blank(values.odometer) ? null : parseKm(values.odometer),
    title: values.title.trim(),
    description: orNull(values.description),
    category: values.category,
    performedBy: orNull(values.performedBy),
    laborCost: blank(values.laborCost) ? null : parseMoney(values.laborCost),
    // Texto digitado antes de voltar para "Manutenção" não segue: o back-end recusaria (422).
    replacedOriginalPart: values.type === 'modification' ? orNull(values.replacedOriginalPart) : null,
    parts: values.parts.map((part) => ({
      name: part.name.trim(),
      brand: orNull(part.brand),
      partNumber: orNull(part.partNumber),
      quantity: parseQuantity(part.quantity) ?? 0,
      unit: part.unit,
      unitPrice: blank(part.unitPrice) ? null : parseMoney(part.unitPrice),
    })),
  }
}

/** Preço × quantidade de uma peça, arredondado em centavos como o back-end (meio centavo para cima). */
export const lineTotal = (part: Pick<PartFormValues, 'quantity' | 'unitPrice'>): number =>
  Math.round((parseQuantity(part.quantity) ?? 0) * (parseMoney(part.unitPrice) ?? 0) * 100) / 100

/** Soma das peças e da mão de obra com o que está digitado agora (campo inválido ou vazio conta zero). */
export const estimateTotal = (laborCost: string, parts: readonly PartFormValues[]): number =>
  Math.round(((parseMoney(laborCost) ?? 0) + parts.reduce((sum, part) => sum + lineTotal(part), 0)) * 100) / 100

// Erros 422 que pertencem a um campo, pelo código.
const FIELD_BY_CODE: Record<string, 'date' | 'odometer'> = {
  'record.date_in_future': 'date',
  'reminder.fulfillment_requires_odometer': 'odometer',
}

const FORM_FIELD =
  /^(type|date|odometer|title|description|category|performedBy|laborCost|replacedOriginalPart|parts\.\d+\.(name|brand|partNumber|quantity|unit|unitPrice))$/

const asKm = (value: unknown) => (typeof value === 'number' || typeof value === 'string' ? value : null)

/**
 * Leva a resposta de erro da API para o formulário: km fora da ordem do histórico mostra a faixa válida no campo da
 * km; erro de validação vai para o campo (peças com índice: `parts[0].name` → `parts.0.name`). Devolve `null` quando
 * tudo virou erro de campo; senão, o texto do aviso geral (o que não é de campo, como o custo total, e o resto).
 */
export function applyRecordProblem(problem: Problem, setError: UseFormSetError<RecordFormValues>): string | null {
  const server = (message: string) => ({ type: 'server', message })

  if (problem.code === 'record.odometer_out_of_order') {
    const range = formatKmRange(asKm(problem.maxKmBefore), asKm(problem.minKmAfter))
    if (range) {
      setError('odometer', server(range), { shouldFocus: true })
      return null
    }
  }

  const fieldByCode = problem.code ? FIELD_BY_CODE[problem.code] : undefined
  if (fieldByCode) {
    setError(fieldByCode, server(problemMessage(problem)), { shouldFocus: true })
    return null
  }

  const general: string[] = []
  let focused = false
  for (const [key, messages] of Object.entries(problem.errors ?? {})) {
    const field = toFieldPath(key)
    if (FORM_FIELD.test(field)) {
      setError(field as Path<RecordFormValues>, server(messages.join(' ')), { shouldFocus: !focused })
      focused = true
    } else {
      general.push(...messages)
    }
  }

  if (general.length > 0) return general.join(' ')
  return focused ? null : problemMessage(problem)
}
