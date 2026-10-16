const REPOSITORY = 'https://github.com/gabrielLAraujo/app-maintenance-history'

type Props = {
  /** Quando a conta demo vence (ISO 8601). */
  expiresAt: string
}

/** Horas que faltam, arredondadas para cima (22 h 59 min viram 23 h); nunca menos de 1. */
function hoursLeft(expiresAt: string): number {
  const millis = new Date(expiresAt).getTime() - Date.now()
  return Math.max(1, Math.ceil(millis / 3_600_000))
}

/** Faixa no topo das telas da conta demo: diz que é uma demonstração e quando a conta some. */
export function DemoBanner({ expiresAt }: Props) {
  return (
    <div
      role="status"
      className="mb-6 flex flex-wrap items-center gap-x-4 gap-y-2 rounded-xl border border-amber-line bg-amber-soft px-4 py-3 text-sm text-[#5C3B00]"
    >
      <svg
        viewBox="0 0 24 24"
        className="size-[18px] flex-none"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden="true"
      >
        <circle cx="12" cy="12" r="9" />
        <path d="M12 8h.01" />
        <path d="M11 12h1v5h1" />
      </svg>
      <span className="min-w-0 flex-[1_1_280px]">
        Você está na demonstração. Pode mexer à vontade: esta conta é apagada em {hoursLeft(expiresAt)} h.
      </span>
      <a href={REPOSITORY} className="font-semibold underline-offset-2 hover:underline">
        Ver o código
      </a>
    </div>
  )
}
