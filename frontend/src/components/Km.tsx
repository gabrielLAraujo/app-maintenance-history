import { formatKm, toNumber } from '../lib/format'

type Props = {
  /** A API manda inteiros como número ou texto (o tipo gerado admite os dois). */
  value: number | string | null | undefined
  className?: string
}

/** Quilometragem em pt-BR ("150.000 km"), com algarismos de largura fixa para alinhar em listas. */
export function Km({ value, className = '' }: Props) {
  return <span className={`font-mono tabular-nums ${className}`}>{formatKm(toNumber(value))}</span>
}
