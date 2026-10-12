export const MAX_KM = 2_000_000

export const INVALID_KM_MESSAGE = 'Informe a km só com números, de 0 a 2.000.000.'

/**
 * O que o usuário digitou no campo de km → número inteiro, ou `null` se não for uma km válida (0 a 2.000.000).
 * Aceita só dígitos ("150000") ou com o ponto de milhar no lugar certo ("150.000"); "187.45" (que parece decimal),
 * vírgula, letras e sinal não passam, para a km não sair 10 vezes maior ou menor sem ninguém notar.
 */
export function parseKm(text: string): number | null {
  const typed = text.trim()
  if (!/^(\d+|\d{1,3}(\.\d{3})+)$/.test(typed)) return null

  const km = Number(typed.replaceAll('.', ''))
  return km <= MAX_KM ? km : null
}

/** Placa como a API guarda: maiúsculas, sem hífen nem espaço (ABC-1D23 → ABC1D23). */
export const normalizePlate = (text: string): string => text.replace(/[-\s]/g, '').toUpperCase()

// Padrão antigo (ABC1234) e Mercosul (ABC1D23), igual ao do back-end.
const PLATE = /^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$/

export const isValidPlate = (text: string): boolean => PLATE.test(normalizePlate(text))
