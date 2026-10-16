import type { ReactNode } from 'react'

type Props = {
  label: string
  /** O `<input type="radio" className="sr-only">` da opção; o rótulo inteiro é o alvo de toque. */
  children: ReactNode
}

/** Uma opção de escolha única em forma de botão grande (44px). A marcada fica âmbar. Use dentro de um `<fieldset>`. */
export function OptionPill({ label, children }: Props) {
  return (
    <label className="flex min-h-11 cursor-pointer items-center justify-center rounded-control bg-surface px-3 text-base font-semibold text-ink-2 ring-1 ring-inset ring-line-strong has-checked:bg-amber-soft has-checked:text-ink has-checked:ring-2 has-checked:ring-amber has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-focus-visible:outline-amber-ink">
      {children}
      {label}
    </label>
  )
}
