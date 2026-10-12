import { describe, expect, it } from 'vitest'
import { isValidPlate, normalizePlate, parseKm, parseMoney, parseQuantity } from './parse'

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

describe('parseMoney', () => {
  it('lê valores em pt-BR: vírgula nos centavos e ponto só de milhar', () => {
    expect(parseMoney('150')).toBe(150)
    expect(parseMoney('150,5')).toBe(150.5)
    expect(parseMoney('45,90')).toBe(45.9)
    expect(parseMoney('1.234,56')).toBe(1234.56)
    expect(parseMoney('1.234')).toBe(1234)
    expect(parseMoney('0,05')).toBe(0.05)
    expect(parseMoney(' R$ 1.038,00 ')).toBe(1038)
  })

  it('aceita até R$ 9.999.999.999,99, como o back-end', () => {
    expect(parseMoney('9.999.999.999,99')).toBe(9_999_999_999.99)
    expect(parseMoney('10.000.000.000')).toBeNull()
  })

  it('recusa o ponto que parece decimal (o valor sairia 100 vezes maior), mais de 2 casas, sinal e letras', () => {
    expect(parseMoney('150.50')).toBeNull()
    expect(parseMoney('1.5')).toBeNull()
    expect(parseMoney('150,555')).toBeNull()
    expect(parseMoney('-10')).toBeNull()
    expect(parseMoney('dez reais')).toBeNull()
    expect(parseMoney('')).toBeNull()
    expect(parseMoney(',50')).toBeNull()
  })
})

describe('parseQuantity', () => {
  it('lê inteiros e decimais com vírgula, até 3 casas', () => {
    expect(parseQuantity('4')).toBe(4)
    expect(parseQuantity('3,5')).toBe(3.5)
    expect(parseQuantity('0,125')).toBe(0.125)
    expect(parseQuantity('99999,999')).toBe(99999.999)
  })

  it('recusa zero, acima do teto, 4 casas, ponto, sinal e vazio', () => {
    expect(parseQuantity('0')).toBeNull()
    expect(parseQuantity('0,000')).toBeNull()
    expect(parseQuantity('100000')).toBeNull()
    expect(parseQuantity('1,2345')).toBeNull()
    expect(parseQuantity('3.5')).toBeNull()
    expect(parseQuantity('-1')).toBeNull()
    expect(parseQuantity('')).toBeNull()
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
