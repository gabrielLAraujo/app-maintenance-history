import { describe, expect, it } from 'vitest'
import { isValidPlate, normalizePlate, parseKm } from './parse'

describe('parseKm', () => {
  it('lê números com ou sem ponto de milhar', () => {
    expect(parseKm('150000')).toBe(150000)
    expect(parseKm('150.000')).toBe(150000)
    expect(parseKm(' 1.234.567 ')).toBe(1234567)
    expect(parseKm('0')).toBe(0)
  })

  it('aceita até 2.000.000 km, como o back-end', () => {
    expect(parseKm('2000000')).toBe(2_000_000)
    expect(parseKm('2000001')).toBeNull()
  })

  it('recusa vazio, vírgula, sinal e letras', () => {
    expect(parseKm('')).toBeNull()
    expect(parseKm('150,5')).toBeNull()
    expect(parseKm('-10')).toBeNull()
    expect(parseKm('12 mil')).toBeNull()
  })

  it('recusa o ponto que não separa milhar (parece decimal, e a km sairia 10 vezes menor)', () => {
    expect(parseKm('187.45')).toBeNull()
    expect(parseKm('1.5')).toBeNull()
    expect(parseKm('1234.567')).toBeNull()
    expect(parseKm('.500')).toBeNull()
  })
})

describe('placa', () => {
  it('normaliza como a API: maiúsculas, sem hífen nem espaço', () => {
    expect(normalizePlate('abc-1d23')).toBe('ABC1D23')
    expect(normalizePlate(' abc 1234 ')).toBe('ABC1234')
  })

  it('aceita o padrão antigo e o Mercosul', () => {
    expect(isValidPlate('ABC-1234')).toBe(true)
    expect(isValidPlate('abc1d23')).toBe(true)
  })

  it('recusa o que não é placa', () => {
    expect(isValidPlate('AB-12345')).toBe(false)
    expect(isValidPlate('ABCD123')).toBe(false)
    expect(isValidPlate('')).toBe(false)
  })
})
