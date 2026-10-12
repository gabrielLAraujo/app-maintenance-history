import { networkProblem, problemMessage, toProblem, type Problem } from './problem'

/** Erro de uma chamada à API: leva o Problem (status, `code`, `detail`, `errors`) para quem trata na tela. */
export class ApiError extends Error {
  readonly problem: Problem

  constructor(problem: Problem) {
    super(problemMessage(problem))
    this.name = 'ApiError'
    this.problem = problem
  }
}

type Outcome<T> = { data?: T; error?: unknown; response: Response }

async function settle<T>(request: Promise<Outcome<T>>): Promise<Outcome<T>> {
  let outcome: Outcome<T>
  try {
    outcome = await request
  } catch {
    // fetch rejeita quando não há resposta (rede fora, API reiniciando).
    throw new ApiError(networkProblem)
  }

  if (!outcome.response.ok) throw new ApiError(toProblem(outcome.response.status, outcome.error))
  return outcome
}

/** Resultado do cliente openapi-fetch → os dados, ou um `ApiError` (que o TanStack Query entrega como `error`). */
export async function unwrap<T>(request: Promise<Outcome<T>>): Promise<T> {
  return (await settle(request)).data as T
}

/** Para respostas sem corpo (204): só confirma que deu certo. */
export async function unwrapEmpty(request: Promise<Outcome<unknown>>): Promise<void> {
  await settle(request)
}

/** O Problem de um erro qualquer capturado numa mutação ou consulta (o que não for do cliente vale como sem rede). */
export const problemOf = (error: unknown): Problem => (error instanceof ApiError ? error.problem : networkProblem)

/** 409 de edição concorrente (`concurrency.conflict`); outro 409, como o dos dados de exemplo, tem mensagem própria. */
export const isConflict = (problem: Problem): boolean => problem.status === 409 && problem.code === 'concurrency.conflict'

/** 503 `catalog.unavailable`: a FIPE não respondeu e não há cache. */
export const isUnavailable = (problem: Problem): boolean => problem.status === 503

export const CONFLICT_MESSAGE = 'Este item foi alterado em outra tela. Os dados foram recarregados; confira e tente de novo.'

/** Texto de um aviso para o erro de uma chamada: o aviso fixo do 409 de concorrência, senão o `detail` da API. */
export const errorMessage = (error: unknown): string => {
  const problem = problemOf(error)
  return isConflict(problem) ? CONFLICT_MESSAGE : problemMessage(problem)
}

/** Repetição das consultas: uma só vez e só para falha de rede ou 5xx. Erro 4xx (404, 401...) não muda ao repetir. */
export const shouldRetry = (failureCount: number, error: unknown): boolean => {
  const { status } = problemOf(error)
  return failureCount < 1 && (status === 0 || status >= 500)
}
