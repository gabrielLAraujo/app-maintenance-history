import { describe, expect, it } from 'vitest'
import { formatDate, formatInstantDate, formatKm, formatMoney, toNumber } from './format'

describe('toNumber', () => {
  it('aceita número e texto numérico (o OpenAPI do .NET admite os dois)', () => {
    expect(toNumber(150000)).toBe(150000)
    expect(toNumber('150000')).toBe(150000)
    expect(toNumber('291.8')).toBe(291.8)
  })

  it('vazio ou inválido vira null', () => {
    expect(toNumber(null)).toBeNull()
    expect(toNumber(undefined)).toBeNull()
    expect(toNumber('')).toBeNull()
    expect(toNumber('abc')).toBeNull()
  })
})

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

describe('formatInstantDate', () => {
  it('mostra o dia em Brasília, não o do UTC', () => {
    // 01:30 UTC de 12/10 ainda é noite de 11/10 em Brasília (UTC-3).
    expect(formatInstantDate('2026-10-12T01:30:00+00:00')).toBe('11/10/2026')
    expect(formatInstantDate('2026-10-12T15:00:00Z')).toBe('12/10/2026')
  })

  it('devolve o texto original quando não é uma data', () => {
    expect(formatInstantDate('ontem')).toBe('ontem')
  })
})
