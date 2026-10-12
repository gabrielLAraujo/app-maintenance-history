import type { UseFormSetError } from 'react-hook-form'
import { describe, expect, it, vi } from 'vitest'
import { applyProblemToForm, toProblem, type Problem } from './problem'

type Form = { email: string; password: string }
type PartsForm = { parts: { name: string }[] }

describe('applyProblemToForm', () => {
  it('validation.failed vira erro nos campos e não sobra mensagem geral', () => {
    const setError = vi.fn<UseFormSetError<Form>>()
    const problem: Problem = {
      status: 400,
      code: 'validation.failed',
      detail: 'Um ou mais erros de validação.',
      errors: { email: ['E-mail já cadastrado.'], password: ['Muito curta.', 'Use mais caracteres.'] },
    }

    const general = applyProblemToForm<Form>(problem, setError)

    expect(general).toBeNull()
    expect(setError).toHaveBeenCalledWith('email', { type: 'server', message: 'E-mail já cadastrado.' })
    expect(setError).toHaveBeenCalledWith('password', { type: 'server', message: 'Muito curta. Use mais caracteres.' })
  })

  it('converte a chave da API (parts[0].name) para o caminho do formulário (parts.0.name)', () => {
    const setError = vi.fn<UseFormSetError<PartsForm>>()

    applyProblemToForm<PartsForm>(
      { status: 400, code: 'validation.failed', errors: { 'parts[0].name': ['Informe o nome.'] } },
      setError,
    )

    expect(setError).toHaveBeenCalledWith('parts.0.name', { type: 'server', message: 'Informe o nome.' })
  })

  it('erro de regra (sem campos) devolve o detail e não toca no formulário', () => {
    const setError = vi.fn<UseFormSetError<Form>>()

    const general = applyProblemToForm<Form>(
      { status: 401, code: 'auth.invalid_credentials', detail: 'E-mail ou senha inválidos.' },
      setError,
    )

    expect(general).toBe('E-mail ou senha inválidos.')
    expect(setError).not.toHaveBeenCalled()
  })

  it('erro de validação sem campo (chave vazia) vira mensagem geral', () => {
    const setError = vi.fn<UseFormSetError<Form>>()

    const general = applyProblemToForm<Form>(
      { status: 400, code: 'validation.failed', detail: 'Corpo inválido.', errors: { '': ['Corpo inválido.'] } },
      setError,
    )

    expect(general).toBe('Corpo inválido.')
    expect(setError).not.toHaveBeenCalled()
  })

  it('sem detail usa uma mensagem padrão pelo status', () => {
    const setError = vi.fn<UseFormSetError<Form>>()

    expect(applyProblemToForm<Form>({ status: 500 }, setError)).toBe(
      'Algo deu errado do nosso lado. Tente de novo em instantes.',
    )
    expect(applyProblemToForm<Form>({ status: 429 }, setError)).toBe('Muitas tentativas. Aguarde um minuto.')
    expect(applyProblemToForm<Form>({ status: 0 }, setError)).toBe(
      'Não foi possível falar com o servidor. Verifique sua conexão e tente de novo.',
    )
  })
})

describe('toProblem', () => {
  it('lê status, code, detail e errors do corpo do ProblemDetails', () => {
    const body = { type: 'x', title: 'Conflict', status: 409, code: 'concurrency.conflict', detail: 'Mudou.', traceId: 't1' }

    expect(toProblem(409, body)).toEqual(body)
  })

  it('o status HTTP da resposta vale mais que o do corpo', () => {
    expect(toProblem(404, { status: 200, code: 'vehicle.not_found' }).status).toBe(404)
  })

  it('corpo que não é objeto (texto, vazio) vira um problema só com o status', () => {
    expect(toProblem(502, 'Bad Gateway')).toEqual({ status: 502 })
    expect(toProblem(500, undefined)).toEqual({ status: 500 })
  })
})
