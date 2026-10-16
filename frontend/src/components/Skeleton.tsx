type Props = { className?: string }

/** Bloco cinza pulsante no lugar do conteúdo que ainda carrega (decorativo: a tela anuncia o carregamento). */
export function Skeleton({ className = '' }: Props) {
  return <div aria-hidden="true" className={`animate-pulse rounded-control bg-track ${className}`} />
}
