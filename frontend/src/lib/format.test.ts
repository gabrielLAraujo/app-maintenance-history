import { describe, expect, it } from 'vitest'
import { formatDate, formatKm, formatMoney } from './format'

describe('formatKm', () => {
  it('formata em pt-BR', () => {
    expect(formatKm(150000)).toBe('150.000 km')
    expect(formatKm(0)).toBe('0 km')
    expect(formatKm(999)).toBe('999 km')
    expect(formatKm(1234567)).toBe('1.234.567 km')
  })

  it('arredonda quilometragem fracionada', () => {
    expect(formatKm(1500.6)).toBe('1.501 km')
  })

  it('mostra traço quando não há valor', () => {
    expect(formatKm(null)).toBe('—')
    expect(formatKm(undefined)).toBe('—')
    expect(formatKm(Number.NaN)).toBe('—')
  })
})

describe('formatMoney', () => {
  it('formata em reais, com espaço comum depois do R$', () => {
    expect(formatMoney(291.8)).toBe('R$ 291,80')
    expect(formatMoney(1234.56)).toBe('R$ 1.234,56')
    expect(formatMoney(0)).toBe('R$ 0,00')
  })

  it('mostra traço quando não há valor', () => {
    expect(formatMoney(null)).toBe('—')
    expect(formatMoney(undefined)).toBe('—')
  })
})

describe('formatDate', () => {
  it('converte yyyy-MM-dd em dd/MM/yyyy sem passar por fuso horário', () => {
    expect(formatDate('2026-10-10')).toBe('10/10/2026')
    expect(formatDate('2026-01-05')).toBe('05/01/2026')
  })

  it('usa só a data de um timestamp ISO', () => {
    expect(formatDate('2026-10-10T23:30:00Z')).toBe('10/10/2026')
  })

  it('devolve o texto original quando não é uma data ISO', () => {
    expect(formatDate('ontem')).toBe('ontem')
  })
})
