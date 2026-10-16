import type { components } from '../../api/schema'
import { Badge } from '../../components/Badge'
import { categoryLabels, recordTypeLabels } from '../../lib/labels'

type Schemas = components['schemas']

/** Tipo do registro: manutenção em cinza, modificação no azul da placa (o texto diz o tipo, a cor só ajuda). */
export function TypeBadge({ type }: { type: Schemas['RecordType'] }) {
  return <Badge tone={type}>{recordTypeLabels[type]}</Badge>
}

export function CategoryBadge({ category }: { category: Schemas['Category'] }) {
  return <Badge tone="neutral">{categoryLabels[category]}</Badge>
}
