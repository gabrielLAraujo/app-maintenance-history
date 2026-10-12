import type { Path, UseFormSetError } from 'react-hook-form'
import { z } from 'zod'
import { problemMessage, toFieldPath, type Problem } from '../../api/problem'
import type { components } from '../../api/schema'
import { todayInSaoPaulo, toNumber } from '../../lib/format'
import {
  INVALID_INTERVAL_KM_MESSAGE,
  INVALID_INTERVAL_MONTHS_MESSAGE,
  INVALID_KM_MESSAGE,
  parseIntervalKm,
  parseIntervalMonths,
  parseKm,
} from '../../lib/parse'

type Schemas = components['schemas']

const blank = (value: string) => value.trim() === ''

/**
 * As mesmas regras do back-end. As que olham dois campos (ao menos um intervalo; km da última vez quando o intervalo é
 * em km) rodam sobre o formulário todo; o `when` as mantém ativas mesmo com outro campo inválido.
 */
export const reminderSchema = z
  .object({
    title: z.string().trim().min(1, 'Informe o título.').max(120, 'O título deve ter no máximo 120 caracteres.'),
    intervalKm: z.string().refine((value) => blank(value) || parseIntervalKm(value) !== null, INVALID_INTERVAL_KM_MESSAGE),
    intervalMonths: z
      .string()
      .refine((value) => blank(value) || parseIntervalMonths(value) !== null, INVALID_INTERVAL_MONTHS_MESSAGE),
    lastDoneDate: z
      .string()
      .regex(/^\d{4}-\d{2}-\d{2}$/, 'Informe a data.')
      .refine((value) => value <= todayInSaoPaulo(), 'A data não pode estar no futuro.'),
    lastDoneOdometer: z.string().refine((value) => blank(value) || parseKm(value) !== null, INVALID_KM_MESSAGE),
  })
  .refine((values) => !blank(values.intervalKm) || !blank(values.intervalMonths), {
    path: ['intervalKm'],
    message: 'Informe o intervalo em km, em meses ou ambos.',
    when: () => true,
  })
  .refine((values) => blank(values.intervalKm) || !blank(values.lastDoneOdometer), {
    path: ['lastDoneOdometer'],
    message: 'Informe a km da última vez para lembretes por km.',
    when: () => true,
  })

/** Valores do formulário: tudo texto, como os campos. */
export type ReminderFormValues = z.infer<typeof reminderSchema>

/** Lembrete novo: a data de hoje e a km atual do veículo (a base mais comum: "acabei de fazer"). */
export const emptyReminderValues = (currentKm: number): ReminderFormValues => ({
  title: '',
  intervalKm: '',
  intervalMonths: '',
  lastDoneDate: todayInSaoPaulo(),
  lastDoneOdometer: String(currentKm),
})

const numberText = (value: number | string | null | undefined) => {
  const number = toNumber(value)
  return number === null ? '' : String(number)
}

/** O lembrete salvo → valores dos campos. */
export const reminderToValues = (reminder: Schemas['ReminderDto']): ReminderFormValues => ({
  title: reminder.title,
  intervalKm: numberText(reminder.intervalKm),
  intervalMonths: numberText(reminder.intervalMonths),
  lastDoneDate: reminder.lastDoneDate,
  lastDoneOdometer: numberText(reminder.lastDoneOdometer),
})

/** Valores já validados → corpo da API: números de verdade (nunca texto) e vazio vira `null`. */
export function valuesToPayload(values: ReminderFormValues): Schemas['CreateReminderRequest'] {
  return {
    title: values.title.trim(),
    intervalKm: blank(values.intervalKm) ? null : parseIntervalKm(values.intervalKm),
    intervalMonths: blank(values.intervalMonths) ? null : parseIntervalMonths(values.intervalMonths),
    lastDoneDate: values.lastDoneDate,
    lastDoneOdometer: blank(values.lastDoneOdometer) ? null : parseKm(values.lastDoneOdometer),
  }
}

// Erros 422 que pertencem a um campo, pelo código.
const FIELD_BY_CODE: Record<string, keyof ReminderFormValues> = {
  'reminder.interval_required': 'intervalKm',
  'reminder.base_date_in_future': 'lastDoneDate',
}

const FORM_FIELD = /^(title|intervalKm|intervalMonths|lastDoneDate|lastDoneOdometer)$/

/**
 * Leva a resposta de erro da API para o formulário: o que é de um campo vai para ele; devolve `null` quando tudo virou
 * erro de campo, senão o texto do aviso geral.
 */
export function applyReminderProblem(problem: Problem, setError: UseFormSetError<ReminderFormValues>): string | null {
  const server = (message: string) => ({ type: 'server', message })

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
      setError(field as Path<ReminderFormValues>, server(messages.join(' ')), { shouldFocus: !focused })
      focused = true
    } else {
      general.push(...messages)
    }
  }

  if (general.length > 0) return general.join(' ')
  return focused ? null : problemMessage(problem)
}
