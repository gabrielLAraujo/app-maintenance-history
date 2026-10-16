import type { ComponentProps } from 'react'

export function Card({ className = '', ...rest }: ComponentProps<'section'>) {
  return <section className={`rounded-card border border-line bg-surface p-4 sm:p-6 ${className}`} {...rest} />
}
