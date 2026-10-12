import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'

/**
 * ProblemDetails da API (RFC 9457) como o front usa: `code` (ex.: `validation.failed`, `concurrency.conflict`) e
 * `traceId` são extensões que o OpenAPI não descreve, por isso o tipo é este e não o gerado.
 */
export type Problem = {
  status: number
  code?: string
  detail?: string
  errors?: Record<string, string[]>
  [k: string]: unknown
}

/** Sem resposta do servidor (rede fora, API reiniciando): não há status HTTP, então vale 0. */
export const networkProblem: Problem = { status: 0, code: 'network.unreachable' }

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value)

/** Monta o Problem a partir do status e do corpo de uma resposta de erro (JSON do ProblemDetails, texto ou vazio). */
export function toProblem(status: number, body: unknown): Problem {
  return { ...(isRecord(body) ? body : {}), status }
}

const defaultMessage = (status: number): string => {
  if (status === 0) return 'Não foi possível falar com o servidor. Verifique sua conexão e tente de novo.'
  if (status === 429) return 'Muitas tentativas. Aguarde um minuto.'
  if (status >= 500) return 'Algo deu errado do nosso lado. Tente de novo em instantes.'
  return 'Não foi possível concluir a operação. Tente de novo.'
}

/** Texto para mostrar num aviso: o `detail` da API (já em pt-BR) ou, na falta dele, um padrão pelo status. */
export const problemMessage = (problem: Problem): string => problem.detail || defaultMessage(problem.status)

// A API manda as chaves em camelCase por segmento ("parts[0].name"); o react-hook-form usa "parts.0.name".
const toFieldPath = (key: string): string => key.replace(/\[(\d+)\]/g, '.$1')

/**
 * Leva os erros de validação (`errors`, por campo) para o formulário com `setError`.
 * Devolve `null` quando todos viraram erro de campo; senão, a mensagem geral para um aviso visível.
 */
export function applyProblemToForm<T extends FieldValues>(problem: Problem, setError: UseFormSetError<T>): string | null {
  let applied = false

  for (const [key, messages] of Object.entries(problem.errors ?? {})) {
    const field = toFieldPath(key)
    if (!field || !Array.isArray(messages) || messages.length === 0) continue

    setError(field as Path<T>, { type: 'server', message: messages.join(' ') })
    applied = true
  }

  return applied ? null : problemMessage(problem)
}
