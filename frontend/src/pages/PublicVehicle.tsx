import { useEffect } from 'react'
import { useParams } from 'react-router'
import { publicAttachmentUrl, usePublicVehicle } from '../api/hooks/public'
import type { components } from '../api/schema'
import { errorMessage, problemOf } from '../api/unwrap'
import { Alert } from '../components/Alert'
import { Button } from '../components/Button'
import { Badge } from '../components/Badge'
import { Card } from '../components/Card'
import { Km } from '../components/Km'
import { Logo } from '../components/Logo'
import { Money } from '../components/Money'
import { PlateChip } from '../components/PlateChip'
import { Spinner } from '../components/Spinner'
import { formatDate, formatKm, formatMoney, formatQuantity, toNumber } from '../lib/format'
import { fuelLabels, partUnitLabels } from '../lib/labels'
import { CategoryBadge, TypeBadge } from './record/RecordBadges'

type Schemas = components['schemas']

/**
 * Enquanto a página pública está aberta: pede aos buscadores para não indexá-la e ao navegador para não mandar o
 * endereço (que leva o token) no `Referer`. As tags saem ao deixar a página, para não valerem no resto do app.
 */
function usePrivatePageMeta() {
  useEffect(() => {
    const tags = [
      ['robots', 'noindex'],
      ['referrer', 'no-referrer'],
    ].map(([name, content]) => {
      const meta = document.createElement('meta')
      meta.name = name
      meta.content = content
      document.head.append(meta)
      return meta
    })

    return () => tags.forEach((meta) => meta.remove())
  }, [])
}

/**
 * Página pública de um link compartilhado (`/p/:token`): só leitura, sem login, sem o menu do app e sem sessão. Mostra o
 * que a API devolve e nada além: custos e placa só aparecem quando vêm (a API manda `null` quando o dono não os liberou).
 */
export function PublicVehicle() {
  const { token = '' } = useParams()
  usePrivatePageMeta()
  const view = usePublicVehicle(token)
  const pages = view.data?.pages ?? []
  const first = pages[0]

  return (
    <div className="min-h-dvh bg-ground text-ink">
      <header className="border-b border-line bg-surface">
        <div className="mx-auto flex max-w-[1080px] flex-wrap items-center gap-x-3 gap-y-2 px-4 py-3 sm:px-6">
          <Logo />
          <span className="text-sm text-muted">Histórico compartilhado</span>
          <Badge tone="maintenance" className="ml-auto gap-1.5 rounded-full px-2.5 py-1">
            <svg
              viewBox="0 0 24 24"
              className="size-3.5"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <rect x="4" y="11" width="16" height="10" rx="2" />
              <path d="M8 11V7a4 4 0 0 1 8 0v4" />
            </svg>
            Só leitura
          </Badge>
        </div>
      </header>

      <main className="mx-auto max-w-[1080px] px-4 py-6 sm:px-6 sm:py-8">
        {view.isPending ? (
          <p className="flex items-center gap-2 py-8 text-muted">
            <Spinner size="sm" label={null} /> Carregando…
          </p>
        ) : !first ? (
          // Sem a primeira página não há o que mostrar. Falha nas seguintes (ou ao atualizar) mantém a tela como está.
          <LinkUnavailable error={view.error} onRetry={() => void view.refetch()} />
        ) : (
          <div className="space-y-6">
            <VehicleHeader vehicle={first} records={pages.flatMap((page) => page.records)} complete={!view.hasNextPage} />
            {first.records.length > 0 && (
              <div className="grid items-start gap-4 lg:grid-cols-[2fr_1fr]">
                <Highlights records={first.records} />
                <NotShown vehicle={first} />
              </div>
            )}
            <section aria-labelledby="history-title">
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <h2 id="history-title" className="text-xl font-bold">
                  Histórico de serviços
                </h2>
                <span className="text-[13px] text-muted">Do mais recente ao mais antigo</span>
              </div>
              <Records
                token={token}
                records={pages.flatMap((page) => page.records)}
                canLoadMore={view.hasNextPage}
                loading={view.isFetchingNextPage}
                failed={view.isFetchNextPageError ? errorMessage(view.error) : null}
                onLoadMore={() => void view.fetchNextPage()}
              />
            </section>
          </div>
        )}
      </main>

      <footer className="mx-auto max-w-[1080px] px-4 pb-8 text-center text-xs text-muted sm:px-6">
        Página somente leitura, compartilhada pelo dono do veículo pela Garagem.
      </footer>
    </div>
  )
}

const MONTHS_SHORT = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez']

/** "2025-12-16" → "dez/2025". */
const monthYear = (iso: string) => `${MONTHS_SHORT[Number(iso.slice(5, 7)) - 1] ?? ''}/${iso.slice(0, 4)}`

const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`

/**
 * Os destaques: até 3 registros, primeiro as modificações (as mais recentes antes) e depois as manutenções mais
 * recentes. Vêm da primeira página (a mais recente), para não mudarem ao carregar mais.
 */
function pickHighlights(records: Schemas['PublicRecordDto'][]): Schemas['PublicRecordDto'][] {
  const modifications = records.filter((record) => record.type === 'modification')
  const maintenances = records.filter((record) => record.type === 'maintenance')
  return [...modifications, ...maintenances].slice(0, 3)
}

function Highlights({ records }: { records: Schemas['PublicRecordDto'][] }) {
  const highlights = pickHighlights(records)

  return (
    <Card aria-labelledby="highlights-title">
      <h2 id="highlights-title" className="text-lg font-bold">
        Destaques do histórico
      </h2>
      <ul className="mt-3 grid gap-3 sm:grid-cols-3">
        {highlights.map((record, index) => {
          const odometer = toNumber(record.odometer)
          return (
            <li key={index} className="rounded-[14px] border border-line bg-ground p-3.5">
              <TypeBadge type={record.type} />
              <h3 className="mt-2 leading-snug font-bold break-words">{record.title}</h3>
              <p className="mt-1 font-mono text-xs text-muted">
                {odometer === null ? monthYear(record.date) : `${monthYear(record.date)} · ${formatKm(odometer)}`}
              </p>
            </li>
          )
        })}
      </ul>
    </Card>
  )
}

/** O que o link esconde: custos e placa (quando o dono não liberou) e o que nunca vai para o link. */
function NotShown({ vehicle }: { vehicle: Schemas['PublicVehicleDto'] }) {
  const hidden = [!vehicle.includesCosts && 'custos', !vehicle.plate && 'placa', 'chassi', 'documentos', 'notas fiscais', 'lembretes'].filter(
    (item): item is string => Boolean(item),
  )
  const sentence = `${hidden.slice(0, -1).join(', ')} e ${hidden[hidden.length - 1]}.`

  return (
    <aside className="flex gap-3 rounded-card border border-plate-line bg-plate-soft p-5 text-plate">
      <svg
        viewBox="0 0 24 24"
        className="mt-0.5 size-5 flex-none"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden="true"
      >
        <path d="M12 3l8 3v6c0 4.5-3.4 8.3-8 9-4.6-.7-8-4.5-8-9V6z" />
      </svg>
      <div>
        <h2 className="font-bold">O que este link não mostra</h2>
        <p className="mt-1 text-sm">{sentence.charAt(0).toUpperCase() + sentence.slice(1)}</p>
        <p className="mt-1 text-sm">O dono pode revogar o link a qualquer momento.</p>
      </div>
    </aside>
  )
}

/**
 * Qualquer falha ao abrir o link mostra o mesmo aviso: inexistente, revogado e expirado são indistinguíveis de propósito
 * (a API responde igual). Só erro do servidor ou de rede, que pode passar sozinho, oferece "Tentar de novo".
 */
function LinkUnavailable({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const status = problemOf(error).status
  const transient = status === 0 || status === 429 || status >= 500

  return (
    <Card className="text-center">
      <h1 className="text-xl font-bold">Link não encontrado ou expirado</h1>
      <p className="mt-2 text-ink-2">Peça um link novo a quem compartilhou o veículo com você.</p>
      {transient && (
        <Button variant="secondary" className="mt-4" onClick={onRetry}>
          Tentar de novo
        </Button>
      )}
    </Card>
  )
}

function VehicleHeader({
  vehicle,
  records,
  complete,
}: {
  vehicle: Schemas['PublicVehicleDto']
  records: Schemas['PublicRecordDto'][]
  /** Todas as páginas carregadas: as contagens valem para o histórico inteiro. */
  complete: boolean
}) {
  const fullName = `${vehicle.make} ${vehicle.model} ${vehicle.modelYear}`
  const title = vehicle.nickname ?? fullName
  const oldest = records.at(-1)
  const photos = records.reduce((sum, record) => sum + record.photos.length, 0)
  const facts = [
    records.length > 0 && (complete ? plural(records.length, 'serviço registrado', 'serviços registrados') : `Mais de ${records.length} serviços`),
    complete && oldest && `Histórico desde ${monthYear(oldest.date)}`,
    photos > 0 && plural(photos, 'foto', 'fotos'),
  ].filter((fact): fact is string => Boolean(fact))

  return (
    <section className="rounded-cover border border-line bg-surface p-4">
      <div className="flex flex-wrap gap-6">
        {vehicle.coverPhoto && (
          <div className="min-w-0 flex-[1_1_420px]">
            <img
              src={vehicle.coverPhoto.path}
              alt={title}
              className="block h-56 w-full rounded-[14px] bg-track object-cover sm:h-[320px]"
            />
          </div>
        )}
        <div className="flex min-w-0 flex-[1_1_360px] flex-col justify-between gap-5 sm:px-2 sm:py-2">
          <div>
            <h1 className="font-display text-[32px] leading-[1.05] sm:text-[40px]">{title}</h1>
            <p className="mt-2 text-ink-2">
              {[vehicle.nickname && fullName, vehicle.generation, vehicle.version, fuelLabels[vehicle.fuel]]
                .filter(Boolean)
                .join(' · ')}
            </p>
            {vehicle.plate && <PlateChip plate={vehicle.plate} className="mt-3" />}
          </div>
          <div className="rounded-[14px] bg-ground px-4 py-4 sm:px-5">
            <p className="text-[13px] font-semibold text-muted">Quilometragem</p>
            <Km value={vehicle.currentOdometer} className="mt-0.5 block text-[30px] leading-tight font-semibold tracking-[-0.02em] sm:text-[34px]" />
          </div>
        </div>
      </div>
      {facts.length > 0 && (
        <ul className="mt-4 flex flex-wrap gap-2 border-t border-track pt-4">
          {facts.map((fact) => (
            <li key={fact} className="rounded-full bg-track px-3 py-1 text-[13px] font-semibold text-ink-2">
              {fact}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

type RecordsProps = {
  token: string
  records: Schemas['PublicRecordDto'][]
  canLoadMore: boolean
  loading: boolean
  /** Mensagem quando a página seguinte falhou (o que já estava na tela continua). */
  failed: string | null
  onLoadMore: () => void
}

function Records({ token, records, canLoadMore, loading, failed, onLoadMore }: RecordsProps) {
  return (
    <div className="mt-3 space-y-3">
      {records.length === 0 ? (
        <p className="rounded-card border-2 border-dashed border-line-strong bg-surface px-4 py-8 text-center text-ink-2">
          Nenhum registro neste link.
        </p>
      ) : (
        <ol className="space-y-3">
          {records.map((record, index) => (
            // A API não manda id do registro (o link não expõe ids internos): a posição na lista é a chave.
            <li key={index}>
              <PublicRecord token={token} record={record} />
            </li>
          ))}
        </ol>
      )}

      {failed && <Alert variant="error">{failed}</Alert>}
      {canLoadMore && (
        <Button variant="secondary" className="w-full" loading={loading} onClick={onLoadMore}>
          Carregar mais
        </Button>
      )}
    </div>
  )
}

function PublicRecord({ token, record }: { token: string; record: Schemas['PublicRecordDto'] }) {
  const odometer = toNumber(record.odometer)
  const labor = toNumber(record.laborCost)
  const total = toNumber(record.totalCost)
  const when = odometer === null ? formatDate(record.date) : `${formatDate(record.date)} · ${formatKm(odometer)}`

  return (
    <article className="rounded-card border border-line bg-surface p-4 sm:p-5">
      <p className="font-mono text-xs font-semibold text-muted">{when}</p>
      <h3 className="mt-1 text-lg leading-snug font-bold break-words">{record.title}</h3>
      <div className="mt-2 flex flex-wrap items-center gap-2">
        <TypeBadge type={record.type} />
        <CategoryBadge category={record.category} />
      </div>

      {record.description && <p className="mt-3 whitespace-pre-line break-words text-ink">{record.description}</p>}
      {(record.performedBy || record.replacedOriginalPart) && (
        <dl className="mt-3 space-y-2">
          {record.performedBy && (
            <div>
              <dt className="text-sm text-muted">Quem fez</dt>
              <dd className="font-medium break-words">{record.performedBy}</dd>
            </div>
          )}
          {record.replacedOriginalPart && (
            <div>
              <dt className="text-sm text-muted">Peça original substituída</dt>
              <dd className="font-medium break-words">{record.replacedOriginalPart}</dd>
            </div>
          )}
        </dl>
      )}

      {record.parts.length > 0 && <Parts parts={record.parts} />}

      {/* Valores só quando a API os mandou. O total de um registro sem custo vem 0: não vira "R$ 0,00". */}
      {(labor !== null || (total !== null && total > 0)) && (
        <dl className="mt-3 space-y-1 border-t border-line pt-3">
          {labor !== null && (
            <div className="flex items-center justify-between gap-3">
              <dt className="text-ink-2">Mão de obra</dt>
              <dd>
                <Money value={labor} className="font-semibold" />
              </dd>
            </div>
          )}
          {total !== null && total > 0 && (
            <div className="flex items-center justify-between gap-3">
              <dt className="font-medium">Total</dt>
              <dd>
                <Money value={total} className="text-lg font-bold" />
              </dd>
            </div>
          )}
        </dl>
      )}

      {record.photos.length > 0 && (
        <ul className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-3">
          {record.photos.map((photo, index) => {
            const url = publicAttachmentUrl(token, photo.id)

            return (
              <li key={photo.id}>
                <a href={url} target="_blank" rel="noopener noreferrer" className="block focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber-ink">
                  <img
                    src={url}
                    alt={`Foto ${index + 1}: ${record.title}`}
                    loading="lazy"
                    decoding="async"
                    className="aspect-square w-full rounded-lg bg-track object-cover"
                  />
                </a>
              </li>
            )
          })}
        </ul>
      )}
    </article>
  )
}

function Parts({ parts }: { parts: Schemas['PublicPartDto'][] }) {
  return (
    <div className="mt-3">
      <h4 className="text-sm font-semibold">Peças</h4>
      <ul className="divide-y divide-track">
        {parts.map((part, index) => {
          const unitPrice = toNumber(part.unitPrice)
          const quantity = `${formatQuantity(toNumber(part.quantity) ?? 0)} ${partUnitLabels[part.unit]}`
          const identity = [part.brand, part.partNumber && `Cód. ${part.partNumber}`].filter(Boolean).join(' · ')

          return (
            <li key={index} className="py-2">
              <p className="font-medium break-words">{part.name}</p>
              {identity && <p className="text-sm text-ink-2 break-words">{identity}</p>}
              <p className="text-sm text-ink-2 tabular-nums">
                {unitPrice === null ? quantity : `${quantity} × ${formatMoney(unitPrice)}`}
              </p>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
