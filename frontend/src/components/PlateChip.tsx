type Props = {
  plate: string
  className?: string
}

/**
 * Placa no padrão Mercosul. O desenho (faixa azul "BRASIL" e letras) é decorativo; o leitor de tela ouve
 * "Placa ABC1D23".
 */
export function PlateChip({ plate, className = '' }: Props) {
  return (
    <span className={`inline-flex ${className}`}>
      <span className="sr-only">Placa {plate}</span>
      <span
        aria-hidden="true"
        className="inline-flex flex-col overflow-hidden rounded-chip border-[1.5px] border-ink bg-surface leading-none"
      >
        <span className="bg-plate py-px text-center text-[7px] font-bold tracking-[0.2em] text-white">BRASIL</span>
        <span className="px-2.5 py-1 font-mono text-[15px] font-semibold tracking-[0.12em] text-ink">{plate}</span>
      </span>
    </span>
  )
}
