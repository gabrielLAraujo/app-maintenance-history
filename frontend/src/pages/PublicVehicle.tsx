import { useEffect } from 'react'
import { useParams } from 'react-router'
import { publicAttachmentUrl, usePublicVehicle } from '../api/hooks/public'
import type { components } from '../api/schema'
import { errorMessage, problemOf } from '../api/unwrap'
import { Alert } from '../components/Alert'
import { Button } from '../components/Button'
import { Card } from '../components/Card'
import { Km } from '../components/Km'
import { PlateChip } from '../components/PlateChip'
import { Money } from '../components/Money'
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
    <div className="min-h-dvh">
      <header className="bg-slate-900 text-white">
        <div className="mx-auto flex max-w-3xl items-baseline gap-3 px-4 py-3">
          <span className="text-lg font-bold tracking-tight">Garagem</span>
          <span className="text-sm text-slate-300">Histórico compartilhado</span>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-4 py-6">
        {view.isPending ? (
          <p className="flex items-center gap-2 py-8 text-slate-600">
            <Spinner size="sm" label={null} /> Carregando…
          </p>
        ) : !first ? (
          // Sem a primeira página não há o que mostrar. Falha nas seguintes (ou ao atualizar) mantém a tela como está.
          <LinkUnavailable error={view.error} onRetry={() => void view.refetch()} />
        ) : (
          <div className="space-y-6">
            <VehicleHeader vehicle={first} />
            <section aria-labelledby="history-title">
              <h2 id="history-title" className="text-lg font-semibold">
                Histórico
              </h2>
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

      <footer className="mx-auto max-w-3xl px-4 pb-8 text-center text-xs text-slate-500">
        Página somente leitura, compartilhada pelo dono do veículo.
      </footer>
    </div>
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
      <h1 className="text-xl font-semibold">Link não encontrado ou expirado</h1>
      <p className="mt-2 text-slate-600">Peça um link novo a quem compartilhou o veículo com você.</p>
      {transient && (
        <Button variant="secondary" className="mt-4" onClick={onRetry}>
          Tentar de novo
        </Button>
      )}
    </Card>
  )
}

function VehicleHeader({ vehicle }: { vehicle: Schemas['PublicVehicleDto'] }) {
  const fullName = `${vehicle.make} ${vehicle.model} ${vehicle.modelYear}`

  return (
    <header>
      <h1 className="text-2xl font-bold tracking-tight">{vehicle.nickname ?? fullName}</h1>
      <p className="mt-1 text-slate-600">
        {[vehicle.nickname && fullName, vehicle.generation, vehicle.version, fuelLabels[vehicle.fuel]].filter(Boolean).join(' · ')}
      </p>
      {vehicle.plate && <PlateChip plate={vehicle.plate} className="mt-2" />}
      <Card className="mt-4">
        <p className="text-sm text-slate-500">Quilometragem</p>
        <Km value={vehicle.currentOdometer} className="block text-2xl font-bold" />
      </Card>
    </header>
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
        <p className="rounded-xl border-2 border-dashed border-slate-300 bg-white px-4 py-8 text-center text-slate-600">
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
    <article className="rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200 sm:p-5">
      <h3 className="font-semibold leading-snug break-words">{record.title}</h3>
      <p className="mt-1 text-sm text-slate-600">{when}</p>
      <div className="mt-2 flex flex-wrap items-center gap-2">
        <TypeBadge type={record.type} />
        <CategoryBadge category={record.category} />
      </div>

      {record.description && <p className="mt-3 whitespace-pre-line break-words text-slate-800">{record.description}</p>}
      {(record.performedBy || record.replacedOriginalPart) && (
        <dl className="mt-3 space-y-2">
          {record.performedBy && (
            <div>
              <dt className="text-sm text-slate-500">Quem fez</dt>
              <dd className="font-medium break-words">{record.performedBy}</dd>
            </div>
          )}
          {record.replacedOriginalPart && (
            <div>
              <dt className="text-sm text-slate-500">Peça original substituída</dt>
              <dd className="font-medium break-words">{record.replacedOriginalPart}</dd>
            </div>
          )}
        </dl>
      )}

      {record.parts.length > 0 && <Parts parts={record.parts} />}

      {/* Valores só quando a API os mandou. O total de um registro sem custo vem 0: não vira "R$ 0,00". */}
      {(labor !== null || (total !== null && total > 0)) && (
        <dl className="mt-3 space-y-1 border-t border-slate-200 pt-3">
          {labor !== null && (
            <div className="flex items-center justify-between gap-3">
              <dt className="text-slate-600">Mão de obra</dt>
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
                <a href={url} target="_blank" rel="noopener noreferrer" className="block focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber-500">
                  <img
                    src={url}
                    alt={`Foto ${index + 1}: ${record.title}`}
                    loading="lazy"
                    decoding="async"
                    className="aspect-square w-full rounded-lg bg-slate-100 object-cover"
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
      <ul className="divide-y divide-slate-100">
        {parts.map((part, index) => {
          const unitPrice = toNumber(part.unitPrice)
          const quantity = `${formatQuantity(toNumber(part.quantity) ?? 0)} ${partUnitLabels[part.unit]}`
          const identity = [part.brand, part.partNumber && `Cód. ${part.partNumber}`].filter(Boolean).join(' · ')

          return (
            <li key={index} className="py-2">
              <p className="font-medium break-words">{part.name}</p>
              {identity && <p className="text-sm text-slate-600 break-words">{identity}</p>}
              <p className="text-sm text-slate-600 tabular-nums">
                {unitPrice === null ? quantity : `${quantity} × ${formatMoney(unitPrice)}`}
              </p>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
