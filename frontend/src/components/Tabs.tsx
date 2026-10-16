import { useId, useRef, type KeyboardEvent, type ReactNode } from 'react'

type Tab<Id extends string> = { id: Id; label: string }

type Props<Id extends string> = {
  /** Nome do conjunto de abas para leitores de tela ("Seções do veículo"). */
  label: string
  tabs: readonly Tab<Id>[]
  value: Id
  onChange: (id: Id) => void
  /** O conteúdo da aba escolhida (só ela é desenhada). */
  children: ReactNode
}

/**
 * Abas no padrão ARIA: setas, Home e End trocam de aba e só a escolhida entra na ordem do Tab. As abas dividem a
 * largura (três cabem em 360px) e o painel mostra só o conteúdo da aba escolhida.
 */
export function Tabs<Id extends string>({ label, tabs, value, onChange, children }: Props<Id>) {
  const base = useId()
  const buttons = useRef(new Map<Id, HTMLButtonElement>())

  function handleKeyDown(event: KeyboardEvent, index: number) {
    const last = tabs.length - 1
    const target =
      event.key === 'ArrowRight' ? (index === last ? 0 : index + 1)
      : event.key === 'ArrowLeft' ? (index === 0 ? last : index - 1)
      : event.key === 'Home' ? 0
      : event.key === 'End' ? last
      : null
    if (target === null) return

    event.preventDefault()
    const next = tabs[target]
    onChange(next.id)
    buttons.current.get(next.id)?.focus()
  }

  return (
    <div>
      <div role="tablist" aria-label={label} className="flex border-b border-line">
        {tabs.map((tab, index) => {
          const selected = tab.id === value

          return (
            <button
              key={tab.id}
              ref={(button) => {
                if (button) buttons.current.set(tab.id, button)
                else buttons.current.delete(tab.id)
              }}
              type="button"
              role="tab"
              id={`${base}-tab-${tab.id}`}
              aria-selected={selected}
              aria-controls={`${base}-panel`}
              tabIndex={selected ? 0 : -1}
              onClick={() => onChange(tab.id)}
              onKeyDown={(event) => handleKeyDown(event, index)}
              className={`-mb-px min-h-12 flex-1 border-b-2 px-2 text-base font-semibold transition-colors focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-amber-ink ${
                selected
                  ? 'border-amber text-ink'
                  : 'border-transparent text-muted hover:border-line-strong hover:text-ink'
              }`}
            >
              {tab.label}
            </button>
          )
        })}
      </div>
      <div role="tabpanel" id={`${base}-panel`} aria-labelledby={`${base}-tab-${value}`} className="pt-4">
        {children}
      </div>
    </div>
  )
}
