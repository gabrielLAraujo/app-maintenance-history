import type { components } from '../../api/schema'
import { categoryLabels, recordTypeLabels } from '../../lib/labels'

type Schemas = components['schemas']

const pill = 'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold'

// A cor distingue o tipo à primeira vista na lista: manutenção azul, modificação violeta.
const typeLook: Record<Schemas['RecordType'], string> = {
  maintenance: 'bg-sky-100 text-sky-900',
  modification: 'bg-violet-100 text-violet-900',
}

export function TypeBadge({ type }: { type: Schemas['RecordType'] }) {
  return <span className={`${pill} ${typeLook[type]}`}>{recordTypeLabels[type]}</span>
}

export function CategoryBadge({ category }: { category: Schemas['Category'] }) {
  return <span className={`${pill} bg-slate-100 text-slate-700`}>{categoryLabels[category]}</span>
}
