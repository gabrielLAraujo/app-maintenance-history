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

/** Teto do `Money` do back-end: R$ 9.999.999.999,99. */
const MAX_MONEY = 9_999_999_999.99

export const INVALID_MONEY_MESSAGE = 'Informe o valor em reais, como 150,00 ou 1.234,56.'

/**
 * O que o usuário digitou num campo de valor → reais (número), ou `null` se não for um valor válido. Segue o pt-BR:
 * vírgula nos centavos (até 2 casas) e, se houver, ponto só como separador de milhar ("1.234,56", "150", "R$ 45,9").
 * "150.50" não passa: um ponto seguido de 2 dígitos parece decimal, e o valor poderia sair 100 vezes maior sem ninguém notar.
 */
export function parseMoney(text: string): number | null {
  const typed = text.trim().replace(/^R\$\s*/, '')
  if (!/^(\d+|\d{1,3}(\.\d{3})+)(,\d{1,2})?$/.test(typed)) return null

  const value = Number(typed.replaceAll('.', '').replace(',', '.'))
  return value <= MAX_MONEY ? value : null
}

export const INVALID_QUANTITY_MESSAGE =
  'A quantidade deve ser maior que zero, com vírgula nos decimais (ex.: 3,5), até 99.999,999.'

/** Quantidade de uma peça: maior que zero, até 99.999,999, com no máximo 3 casas e vírgula como separador decimal. */
export function parseQuantity(text: string): number | null {
  const typed = text.trim()
  if (!/^\d{1,5}(,\d{1,3})?$/.test(typed)) return null

  const value = Number(typed.replace(',', '.'))
  return value > 0 ? value : null
}

/** Placa como a API guarda: maiúsculas, sem hífen nem espaço (ABC-1D23 → ABC1D23). */
export const normalizePlate = (text: string): string => text.replace(/[-\s]/g, '').toUpperCase()

// Padrão antigo (ABC1234) e Mercosul (ABC1D23), igual ao do back-end.
const PLATE = /^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$/

export const isValidPlate = (text: string): boolean => PLATE.test(normalizePlate(text))
