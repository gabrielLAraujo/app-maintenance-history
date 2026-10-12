import type { ComponentProps } from 'react'

export function Card({ className = '', ...rest }: ComponentProps<'section'>) {
  return <section className={`rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200 sm:p-6 ${className}`} {...rest} />
}
