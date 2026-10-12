type Props = {
  size?: 'sm' | 'md'
  /** Texto para leitores de tela. `null` esconde o spinner deles (quando o botão ou a tela já dizem o que acontece). */
  label?: string | null
}

const sizes = { sm: 'size-4', md: 'size-8' }

export function Spinner({ size = 'md', label = 'Carregando' }: Props) {
  return (
    <span role={label === null ? undefined : 'status'} className="inline-flex">
      <svg
        className={`${sizes[size]} animate-spin text-current`}
        viewBox="0 0 24 24"
        fill="none"
        aria-hidden="true"
      >
        <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="3" className="opacity-25" />
        <path d="M21 12a9 9 0 0 0-9-9" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
      </svg>
      {label !== null && <span className="sr-only">{label}</span>}
    </span>
  )
}
