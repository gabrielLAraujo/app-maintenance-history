import { Link, useSearchParams } from 'react-router'
import { useRecords } from '../../api/hooks/records'
import type { components } from '../../api/schema'
import { errorMessage } from '../../api/unwrap'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { ButtonLink } from '../../components/ButtonLink'
import { Empty } from '../../components/Empty'
import { Km } from '../../components/Km'
import { Money } from '../../components/Money'
import { SelectField } from '../../components/SelectField'
import { Spinner } from '../../components/Spinner'
import { formatDate, toNumber } from '../../lib/format'
import { categories, categoryLabels, recordTypeLabels, recordTypes } from '../../lib/labels'
import { CategoryBadge, TypeBadge } from '../record/RecordBadges'

type Schemas = components['schemas']

/**
 * Histórico do veículo: filtros por tipo e categoria (ficam no endereço, então o "voltar" do detalhe do registro
 * reencontra a lista filtrada) e os registros do mais novo para o mais antigo, uma página por vez.
 */
export function HistoryTab({ vehicleId }: { vehicleId: string }) {
  const [params, setParams] = useSearchParams()
  // Valor desconhecido no endereço (digitado à mão, ou de uma versão antiga) vale como "sem filtro".
  const type = recordTypes.find((value) => value === params.get('type'))
  const category = categories.find((value) => value === params.get('category'))
  const filtered = type !== undefined || category !== undefined
  const records = useRecords(vehicleId, { type, category })
  const items = records.data?.pages.flatMap((page) => page.items) ?? []
  const newRecordPath = `/vehicles/${vehicleId}/records/new`

  // Mexe só nos filtros: o resto do endereço (a aba aberta, por exemplo) fica como está.
  function setFilters(changes: Record<'type' | 'category', string | null>) {
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        for (const [name, value] of Object.entries(changes)) {
          if (value) next.set(name, value)
          else next.delete(name)
        }
        return next
      },
      { replace: true },
    )
  }

  if (records.isPending) {
    return (
      <p className="flex items-center gap-2 py-8 text-muted">
        <Spinner size="sm" label={null} /> Carregando histórico…
      </p>
    )
  }

  if (items.length === 0 && !filtered && !records.error) {
    return (
      <Empty
        title="Nenhum registro ainda"
        actions={<ButtonLink to={newRecordPath}>Registrar o primeiro serviço</ButtonLink>}
      >
        <p>Anote trocas de óleo, peças e modificações. Dá para anexar fotos e notas fiscais a cada registro.</p>
      </Empty>
    )
  }

  return (
    <div className="space-y-4">
      {/* "Novo registro" fica no cabeçalho do veículo, sempre à vista. */}
      <div className="grid grid-cols-2 gap-3 sm:max-w-md">
        <SelectField label="Tipo" value={type ?? ''} onChange={(event) => setFilters({ type: event.target.value, category: category ?? null })}>
          <option value="">Todos</option>
          {recordTypes.map((value) => (
            <option key={value} value={value}>
              {recordTypeLabels[value]}
            </option>
          ))}
        </SelectField>
        <SelectField
          label="Categoria"
          value={category ?? ''}
          onChange={(event) => setFilters({ type: type ?? null, category: event.target.value })}
        >
          <option value="">Todas</option>
          {categories.map((value) => (
            <option key={value} value={value}>
              {categoryLabels[value]}
            </option>
          ))}
        </SelectField>
      </div>

      {records.error && (
        <div className="space-y-3">
          <Alert variant="error">{errorMessage(records.error)}</Alert>
          {items.length === 0 && (
            <Button variant="secondary" onClick={() => void records.refetch()}>
              Tentar de novo
            </Button>
          )}
        </div>
      )}

      {items.length > 0 && (
        // Troca de filtro: a lista anterior fica (apagada) até a nova chegar, em vez de sumir e pular a tela.
        <ul className={`transition-opacity ${records.isPlaceholderData ? 'opacity-50' : ''}`} aria-busy={records.isPlaceholderData}>
          {items.map((item) => (
            <RecordRow key={item.id} vehicleId={vehicleId} item={item} />
          ))}
        </ul>
      )}

      {items.length === 0 && !records.error && (
        <div className="rounded-card border-2 border-dashed border-line-strong bg-surface px-4 py-8 text-center">
          <p className="font-semibold">Nenhum registro com esses filtros.</p>
          <Button variant="secondary" className="mt-4" onClick={() => setFilters({ type: null, category: null })}>
            Limpar filtros
          </Button>
        </div>
      )}

      {records.hasNextPage && (
        <Button
          variant="secondary"
          className="w-full"
          loading={records.isFetchingNextPage}
          onClick={() => void records.fetchNextPage()}
        >
          Carregar mais
        </Button>
      )}
    </div>
  )
}

function ClipIcon() {
  return (
    <svg viewBox="0 0 24 24" className="size-4" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="m21 11-8.6 8.6a5 5 0 0 1-7-7l8.6-8.6a3.4 3.4 0 0 1 4.8 4.8l-8.6 8.6a1.7 1.7 0 0 1-2.4-2.4L15 6.6" />
    </svg>
  )
}

/** Um registro na linha do tempo: data e km em mono à esquerda, o marco âmbar e o cartão do serviço. */
function RecordRow({ vehicleId, item }: { vehicleId: string; item: Schemas['RecordListItemDto'] }) {
  const odometer = toNumber(item.odometer)
  const total = toNumber(item.totalCost) ?? 0
  const attachments = toNumber(item.attachmentCount) ?? 0

  return (
    <li className="flex gap-3 sm:gap-4">
      <div className="w-[84px] flex-none pt-4 text-right sm:w-24">
        <p className="font-mono text-xs font-semibold text-ink">{formatDate(item.date)}</p>
        {odometer !== null && <Km value={odometer} className="block text-xs text-muted" />}
      </div>
      <div aria-hidden="true" className="flex w-3.5 flex-none flex-col items-center">
        <span className="mt-[18px] size-3 rounded-full border-2 border-ink bg-amber" />
        <span className="w-0.5 flex-1 bg-line" />
      </div>
      <Link
        to={`/vehicles/${vehicleId}/records/${item.id}`}
        className="mb-3.5 block min-w-0 flex-1 rounded-[14px] border border-line bg-surface p-4 transition hover:border-line-strong hover:shadow-[0_8px_24px_rgba(20,22,27,0.08)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ink"
      >
        <div className="flex items-start justify-between gap-3">
          <h3 className="min-w-0 leading-snug font-bold break-words">{item.title}</h3>
          {total > 0 && <Money value={total} className="shrink-0 font-semibold" />}
        </div>
        <div className="mt-2 flex flex-wrap items-center gap-2">
          <TypeBadge type={item.type} />
          <CategoryBadge category={item.category} />
          {attachments > 0 && (
            <span className="inline-flex items-center gap-1 text-xs font-medium text-muted">
              <ClipIcon />
              <span aria-hidden="true">{attachments}</span>
              <span className="sr-only">{attachments === 1 ? '1 anexo' : `${attachments} anexos`}</span>
            </span>
          )}
        </div>
      </Link>
    </li>
  )
}
