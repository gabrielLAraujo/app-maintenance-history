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

/**
 * A faixa de km que o histórico aceita num registro (`record.odometer_out_of_order`): `maxKmBefore` é a maior km dos
 * registros anteriores (o piso) e `minKmAfter`, a menor km dos posteriores (o teto). Qualquer um pode faltar.
 */
export function formatKmRange(
  maxKmBefore: number | string | null | undefined,
  minKmAfter: number | string | null | undefined,
): string | null {
  const floor = toNumber(maxKmBefore)
  const ceiling = toNumber(minKmAfter)
  const plain = (km: number) => kmFormat.format(km)

  if (floor !== null && ceiling !== null) return `Entre ${plain(floor)} e ${plain(ceiling)} km`
  if (floor !== null) return `Acima de ${plain(floor)} km`
  if (ceiling !== null) return `Abaixo de ${plain(ceiling)} km`
  return null
}

const moneyInputFormat = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const quantityFormat = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3, useGrouping: false })

/** Valor para um campo de texto de reais, sem o "R$": 4250 → "4.250,00" (o `parseMoney` lê de volta). */
export const formatMoneyInput = (value: number): string => moneyInputFormat.format(value)

/** Quantidade para um campo de texto: 3.5 → "3,5", 4 → "4" (o `parseQuantity` lê de volta). */
export const formatQuantity = (value: number): string => quantityFormat.format(value)

/** Tamanho de arquivo para a tela: 850 KB, 1,2 MB. */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`
  return `${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 }).format(bytes / (1024 * 1024))} MB`
}

const todayParts = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'America/Sao_Paulo',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

/** O "hoje" da API (fuso America/Sao_Paulo) como yyyy-MM-dd, o formato do campo de data. */
export function todayInSaoPaulo(): string {
  const parts = todayParts.formatToParts(new Date())
  const part = (type: string) => parts.find((p) => p.type === type)?.value ?? ''
  return `${part('year')}-${part('month')}-${part('day')}`
}

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

const dayMonthFormat = new Intl.DateTimeFormat('pt-BR', { timeZone: 'America/Sao_Paulo', day: '2-digit', month: '2-digit' })
const yearFormat = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo', year: 'numeric' })

/**
 * Momento exato → "dd/MM" em Brasília. De outro ano vai completo (dd/MM/yyyy): "02/12" sem ano, lido em janeiro,
 * pareceria recente. `now` existe para os testes.
 */
export const formatInstantDayMonth = (iso: string, now: Date = new Date()): string => {
  const instant = new Date(iso)
  if (Number.isNaN(instant.getTime())) return iso

  return yearFormat.format(instant) === yearFormat.format(now) ? dayMonthFormat.format(instant) : instantFormat.format(instant)
}

const offsetFormat = new Intl.DateTimeFormat('en-US', { timeZone: 'America/Sao_Paulo', timeZoneName: 'longOffset' })

/**
 * "2026-12-31" → o último segundo desse dia em Brasília como instante ISO com deslocamento
 * ("2026-12-31T23:59:59-03:00"), o formato que a API aceita em `expiresAt`. O deslocamento vem do fuso (não é fixo),
 * então acompanha uma eventual volta do horário de verão.
 */
export function endOfDayInSaoPaulo(date: string): string {
  // Ao meio-dia UTC já é o mesmo dia civil em Brasília (UTC-2 a UTC-3), e longe de qualquer virada de horário.
  const label = offsetFormat.formatToParts(new Date(`${date}T12:00:00Z`)).find((part) => part.type === 'timeZoneName')?.value
  const offset = /^GMT([+-]\d{2}:\d{2})$/.exec(label ?? '')?.[1] ?? '-03:00'
  return `${date}T23:59:59${offset}`
}
