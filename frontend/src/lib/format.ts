const EMPTY = '—'

const kmFormat = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 })
const moneyFormat = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

const hasValue = (value: number | null | undefined): value is number =>
  typeof value === 'number' && Number.isFinite(value)

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
