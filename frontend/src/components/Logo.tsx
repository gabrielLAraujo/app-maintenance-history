type Props = {
  /** Sobre fundo escuro (barra lateral): o nome em branco. */
  inverse?: boolean
  className?: string
}

/** Marca da Garagem: o mostrador âmbar num quadrado grafite e o nome em Archivo larga. */
export function Logo({ inverse = false, className = '' }: Props) {
  return (
    <span className={`inline-flex items-center gap-2.5 ${className}`}>
      <span className="inline-flex size-9 items-center justify-center rounded-control bg-ink ring-1 ring-white/10">
        <svg
          viewBox="0 0 24 24"
          className="size-[22px]"
          fill="none"
          stroke="#F5A524"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
        >
          <path d="M4.5 17a8.5 8.5 0 1 1 15 0" />
          <path d="M12 13.5l4-4.5" />
          <circle cx="12" cy="14" r="1.2" fill="#F5A524" />
        </svg>
      </span>
      <span className={`text-[21px] font-extrabold tracking-[-0.01em] [font-stretch:118%] ${inverse ? 'text-white' : 'text-ink'}`}>
        Garagem
      </span>
    </span>
  )
}
