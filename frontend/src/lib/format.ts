const EMPTY = '—'

const kmFormat = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 })
const moneyFormat = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

const hasValue = (value: number | null | undefined): value is number =>
  typeof value === 'number' && Number.isFinite(value)

/** O OpenAPI do .NET admite número ou texto em inteiros e decimais; aqui vira número (vazio ou inválido → null). */
export const toNumber = (value: number | string | null | undefined): number | null => {
  if (value === null || value === undefined || value === '') return null

  const number = Number(value)
  return Number.isFinite(number) ? number : null
}

/** 150000 → "150.000 km"; sem valor → "—". */
export const formatKm = (km: number | null | undefined): string =>
  hasValue(km) ? `${kmFormat.format(km)} km` : EMPTY

/** 291.8 → "R$ 291,80"; sem valor → "—". O Intl separa o "R$" com espaço não separável; aqui vai um espaço comum. */
export const formatMoney = (value: number | null | undefined): string =>
  hasValue(value) ? moneyFormat.format(value).replace(/ /g, ' ') : EMPTY

const ISO_DATE = /^(\d{4})-(\d{2})-(\d{2})/

/**
 * "2026-10-10" → "10/10/2026". Troca as partes do texto em vez de criar um Date: a data da API não tem hora nem
 * fuso, e `new Date('2026-10-10')` (UTC) mostraria o dia anterior em Brasília.
 */
export const formatDate = (iso: string): string => {
  const match = ISO_DATE.exec(iso)
  return match ? `${match[3]}/${match[2]}/${match[1]}` : iso
}

const instantFormat = new Intl.DateTimeFormat('pt-BR', {
  timeZone: 'America/Sao_Paulo',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

/** Momento exato (data e hora com fuso, ex.: "2026-10-12T01:30:00+00:00") → dia em Brasília, dd/MM/yyyy. */
export const formatInstantDate = (iso: string): string => {
  const instant = new Date(iso)
  return Number.isNaN(instant.getTime()) ? iso : instantFormat.format(instant)
}
