import { describe, expect, it } from 'vitest'
import { intervalSummary, isOdometerStale, statusLines } from './reminderStatus'

const status = (kmRemaining: number | null, daysRemaining: number | null) => ({
  state: 'ok' as const,
  kmRemaining,
  daysRemaining,
  odometerUpdatedAt: '2026-10-12T00:00:00+00:00',
})

describe('statusLines', () => {
  it('positivo falta, com singular e milhar', () => {
    expect(statusLines(status(500, 40)).map((line) => line.text)).toEqual(['Faltam 500 km', 'Faltam 40 dias'])
    expect(statusLines(status(1, 1)).map((line) => line.text)).toEqual(['Falta 1 km', 'Falta 1 dia'])
    expect(statusLines(status(42000, null)).map((line) => line.text)).toEqual(['Faltam 42.000 km'])
  })

  it('zero vence agora (km) e vence hoje (tempo); negativo já passou', () => {
    expect(statusLines(status(0, 0)).map((line) => line.text)).toEqual(['Vence agora', 'Vence hoje'])
    expect(statusLines(status(-300, -10)).map((line) => line.text)).toEqual(['Passou 300 km', 'Venceu há 10 dias'])
    expect(statusLines(status(null, -1)).map((line) => line.text)).toEqual(['Venceu há 1 dia'])
  })

  it('só o eixo que ainda tem valor vira linha (lembrete só por km, só por tempo)', () => {
    expect(statusLines(status(500, null))).toEqual([{ axis: 'km', label: 'Por km', text: 'Faltam 500 km', late: false }])
    expect(statusLines(status(null, 5))).toEqual([{ axis: 'time', label: 'Por tempo', text: 'Faltam 5 dias', late: false }])
    expect(statusLines(status(null, null))).toEqual([])
  })

  it('late marca o eixo que já venceu (≤ 0), não o que ainda tem folga', () => {
    expect(statusLines(status(5000, -10)).map((line) => line.late)).toEqual([false, true])
    expect(statusLines(status(0, 30)).map((line) => line.late)).toEqual([true, false])
  })

  it('aceita o número como texto (o OpenAPI do .NET admite os dois)', () => {
    expect(statusLines({ ...status(null, null), kmRemaining: '500', daysRemaining: '-10' }).map((line) => line.text)).toEqual([
      'Faltam 500 km',
      'Venceu há 10 dias',
    ])
  })
})

describe('intervalSummary', () => {
  it('km, meses ou os dois (vale o que vier primeiro)', () => {
    expect(intervalSummary({ intervalKm: 10000, intervalMonths: 12 })).toBe('A cada 10.000 km ou 12 meses')
    expect(intervalSummary({ intervalKm: 10000, intervalMonths: null })).toBe('A cada 10.000 km')
    expect(intervalSummary({ intervalKm: null, intervalMonths: 24 })).toBe('A cada 24 meses')
    expect(intervalSummary({ intervalKm: null, intervalMonths: 1 })).toBe('A cada 1 mês')
  })
})

describe('isOdometerStale', () => {
  const now = new Date('2026-10-12T15:00:00Z')

  it('mais de 30 dias é velha; exatamente 30 ainda não', () => {
    expect(isOdometerStale('2026-09-12T15:00:00Z', now)).toBe(false) // 30 dias
    expect(isOdometerStale('2026-09-12T14:59:59Z', now)).toBe(true) // 30 dias e 1 segundo
    expect(isOdometerStale('2026-08-02T15:00:00Z', now)).toBe(true)
  })

  it('km atualizada hoje ou há pouco não é velha', () => {
    expect(isOdometerStale('2026-10-12T14:00:00Z', now)).toBe(false)
    expect(isOdometerStale('2026-10-01T00:00:00Z', now)).toBe(false)
  })

  it('data inválida não avisa', () => {
    expect(isOdometerStale('ontem', now)).toBe(false)
  })
})
