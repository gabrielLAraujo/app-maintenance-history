import { formatMoney, toNumber } from '../lib/format'

type Props = {
  /** A API manda decimais como número ou texto (o tipo gerado admite os dois). */
  value: number | string | null | undefined
  className?: string
}

/** Valor em reais ("R$ 1.234,56"), com algarismos de largura fixa para alinhar em listas. */
export function Money({ value, className = '' }: Props) {
  return <span className={`tabular-nums ${className}`}>{formatMoney(toNumber(value))}</span>
}
