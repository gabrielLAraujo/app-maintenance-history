import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { coverUrl, useVehicleSummary } from '../api/hooks/summary'
import { useFipePrice, useVehicle } from '../api/hooks/vehicles'
import { Button } from '../components/Button'
import { ButtonLink } from '../components/ButtonLink'
import { Kpi } from '../components/Kpi'
import { Km } from '../components/Km'
import { Money } from '../components/Money'
import { PlateChip } from '../components/PlateChip'
import { Skeleton } from '../components/Skeleton'
import { Spinner } from '../components/Spinner'
import { Tabs } from '../components/Tabs'
import { formatInstantDate, formatKm, formatMoney, toNumber } from '../lib/format'
import { fuelLabels } from '../lib/labels'
import { HistoryTab } from './vehicle/HistoryTab'
import { OdometerDialog } from './vehicle/OdometerDialog'
import { RemindersTab } from './vehicle/RemindersTab'
import { ShareTab } from './vehicle/ShareTab'
import { SpendingTab } from './vehicle/SpendingTab'
import { VehicleLoadError } from './vehicle/VehicleLoadError'

const TABS = [
  { id: 'history', label: 'Histórico' },
  { id: 'reminders', label: 'Lembretes' },
  { id: 'spending', label: 'Gastos' },
  { id: 'share', label: 'Compartilhar' },
] as const

const MONTHS_SHORT = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez']

/** "2024-11-11" → "nov/2024". */
const monthYear = (iso: string) => `${MONTHS_SHORT[Number(iso.slice(5, 7)) - 1] ?? ''}/${iso.slice(0, 4)}`

const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`

/** Os quatro números do veículo: gasto total, manutenção, modificações e custo por km (só com 1.000 km ou mais). */
function VehicleNumbers({ vehicleId }: { vehicleId: string }) {
  const summary = useVehicleSummary(vehicleId)

  return (
    <section aria-label="Gastos do veículo" className="mt-4 grid grid-cols-2 gap-3 sm:gap-4 xl:grid-cols-4">
      {summary.data ? (
        <>
          <Kpi
            label="Gasto total"
            value={formatMoney(toNumber(summary.data.totalCost))}
            hint={
              summary.data.firstRecordDate
                ? `${plural(
                    (toNumber(summary.data.maintenance.count) ?? 0) + (toNumber(summary.data.modification.count) ?? 0),
                    'registro',
                    'registros',
                  )} desde ${monthYear(summary.data.firstRecordDate)}`
                : 'nenhum registro ainda'
            }
          />
          <Kpi
            label="Manutenção"
            value={formatMoney(toNumber(summary.data.maintenance.cost))}
            hint={plural(toNumber(summary.data.maintenance.count) ?? 0, 'serviço', 'serviços')}
          />
          <Kpi
            label="Modificações"
            value={formatMoney(toNumber(summary.data.modification.cost))}
            hint={plural(toNumber(summary.data.modification.count) ?? 0, 'modificação', 'modificações')}
          />
          <Kpi
            label="Custo por km"
            value={summary.data.costPerKm === null ? '—' : formatMoney(toNumber(summary.data.costPerKm))}
            hint={
              summary.data.costPerKm === null
                ? 'a partir de 1.000 km acompanhados'
                : `em ${formatKm(toNumber(summary.data.kmTracked))} acompanhados`
            }
          />
        </>
      ) : summary.error ? (
        <p className="col-span-full text-sm text-muted">Não foi possível carregar os gastos deste veículo.</p>
      ) : (
        Array.from({ length: 4 }, (_, index) => <Skeleton key={index} className="h-[106px] rounded-card" />)
      )}
    </section>
  )
}

type TabId = (typeof TABS)[number]['id']

/**
 * Tela do veículo: capa (ou o convite para escolher uma), nome, placa, km (e o valor FIPE, se vinculado), os números de
 * gasto e as abas Histórico, Lembretes, Gastos e Compartilhar.
 */
export function VehicleDetail() {
  const { id = '' } = useParams()
  const vehicle = useVehicle(id)
  const [params, setParams] = useSearchParams()
  const [odometerDialog, setOdometerDialog] = useState<'update' | 'correct' | null>(null)
  const linked = vehicle.data?.catalog != null
  const fipe = useFipePrice(id, linked)

  if (vehicle.isPending) {
    return (
      <p className="flex items-center gap-2 py-8 text-muted">
        <Spinner size="sm" label={null} /> Carregando veículo…
      </p>
    )
  }
  if (vehicle.error || !vehicle.data) return <VehicleLoadError error={vehicle.error} onRetry={() => void vehicle.refetch()} />

  const current = vehicle.data
  const fullName = `${current.make} ${current.model} ${current.modelYear}`
  // A aba fica no endereço (?tab=reminders): o "voltar" do navegador e os links de outras telas caem na aba certa.
  const tab: TabId = TABS.find((candidate) => candidate.id === params.get('tab'))?.id ?? 'history'

  function selectTab(next: TabId) {
    setParams(
      (existing) => {
        const updated = new URLSearchParams(existing)
        if (next === 'history') updated.delete('tab')
        else updated.set('tab', next)
        // Os filtros do histórico não fazem sentido nas outras abas.
        updated.delete('type')
        updated.delete('category')
        return updated
      },
      { replace: true },
    )
  }

  const title = current.nickname ?? fullName
  const subtitle = [current.nickname && fullName, current.version, fuelLabels[current.fuel]].filter(Boolean).join(' · ')

  return (
    <div>
      <nav aria-label="Trilha" className="mb-3 flex items-center gap-2 text-sm text-muted">
        <Link to="/vehicles" className="inline-flex min-h-11 items-center font-semibold hover:text-ink">
          Veículos
        </Link>
        <span aria-hidden="true">/</span>
        <span aria-current="page" className="truncate text-ink">
          {title}
        </span>
      </nav>

      <section className="flex flex-wrap gap-6 rounded-cover border border-line bg-surface p-4">
        <div className="min-w-0 flex-[1_1_420px]">
          {current.coverPhotoId ? (
            <img
              src={coverUrl(id, current.coverPhotoId)}
              alt={title}
              className="block h-56 w-full rounded-[14px] bg-track object-cover sm:h-[300px]"
            />
          ) : (
            // A capa sai das fotos dos registros (sem upload próprio): a caixa explica onde escolher, sem ser um link que,
            // na aba Histórico, não levaria a lugar nenhum.
            <div className="flex h-56 w-full flex-col items-center justify-center gap-2 rounded-[14px] bg-track px-4 text-center text-muted sm:h-[300px]">
              <svg
                viewBox="0 0 24 24"
                className="size-8"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.6"
                strokeLinecap="round"
                strokeLinejoin="round"
                aria-hidden="true"
              >
                <path d="M4 7h3l2-3h6l2 3h3a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V8a1 1 0 0 1 1-1z" />
                <circle cx="12" cy="13" r="4" />
              </svg>
              <span className="font-semibold text-ink">Sem foto de capa</span>
              <span className="max-w-xs text-[13px]">Abra um registro com foto no histórico e escolha "Usar como capa".</span>
            </div>
          )}
        </div>

        <div className="flex min-w-0 flex-[1_1_380px] flex-col gap-4 sm:px-2 sm:py-3">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="min-w-0 font-display text-[32px] leading-[1.05] sm:text-[38px]">{title}</h1>
            {current.plate && <PlateChip plate={current.plate} />}
          </div>
          {subtitle && <p className="text-ink-2">{subtitle}</p>}

          <div className="flex flex-wrap items-center justify-between gap-3 rounded-[14px] bg-ground px-4 py-4 sm:px-5">
            <div>
              <p className="text-[13px] font-semibold text-muted">Quilometragem</p>
              <Km value={current.currentOdometer} className="mt-0.5 block text-[30px] leading-tight font-semibold tracking-[-0.02em] sm:text-[34px]" />
              <p className="mt-1 text-[13px] text-muted">
                <span>atualizada em {formatInstantDate(current.odometerUpdatedAt)}</span>
                <span aria-hidden="true" className="hidden sm:inline">
                  {' · '}
                </span>
                <button
                  type="button"
                  onClick={() => setOdometerDialog('correct')}
                  className="block min-h-11 text-ink-2 underline underline-offset-2 hover:text-ink sm:inline sm:min-h-0"
                >
                  Corrigir km digitada errado
                </button>
              </p>
            </div>
            <Button onClick={() => setOdometerDialog('update')}>Atualizar km</Button>
          </div>

          {linked && (
            <div className="flex items-center justify-between gap-3 border-t border-track pt-3">
              <div>
                <p className="text-[13px] font-semibold text-muted">Tabela FIPE</p>
                {fipe.isPending ? (
                  <p className="text-sm text-muted">Consultando…</p>
                ) : fipe.data ? (
                  <>
                    <Money value={fipe.data.price} className="block text-xl font-semibold" />
                    <p className="text-[13px] text-muted">referência {fipe.data.referenceMonth}</p>
                  </>
                ) : (
                  // 503 (FIPE fora do ar), limite de consultas ou qualquer outra falha: o valor é um extra, nunca trava a tela.
                  <p className="text-sm text-muted">Valor indisponível agora.</p>
                )}
              </div>
              {fipe.isError && (
                <Button variant="secondary" onClick={() => void fipe.refetch()}>
                  Tentar de novo
                </Button>
              )}
            </div>
          )}

          <div className="flex flex-wrap gap-2.5">
            <ButtonLink to={`/vehicles/${id}/records/new`} variant="dark">
              <svg viewBox="0 0 24 24" className="size-[18px]" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" aria-hidden="true">
                <path d="M12 5v14" />
                <path d="M5 12h14" />
              </svg>
              Novo registro
            </ButtonLink>
            <Button variant="secondary" onClick={() => selectTab('share')}>
              Compartilhar
            </Button>
            <ButtonLink to={`/vehicles/${id}/edit`} variant="ghost">
              Editar
            </ButtonLink>
          </div>
        </div>
      </section>

      <VehicleNumbers vehicleId={id} />

      <div className="mt-6">
        <Tabs label="Seções do veículo" tabs={TABS} value={tab} onChange={selectTab}>
          {tab === 'history' && <HistoryTab key={id} vehicleId={id} />}
          {tab === 'reminders' && (
            <RemindersTab
              key={id}
              vehicleId={id}
              currentKm={toNumber(current.currentOdometer) ?? 0}
              odometerUpdatedAt={current.odometerUpdatedAt}
              onUpdateKm={() => setOdometerDialog('update')}
            />
          )}
          {tab === 'spending' && <SpendingTab key={id} vehicleId={id} />}
          {tab === 'share' && <ShareTab key={id} vehicleId={id} />}
        </Tabs>
      </div>

      {odometerDialog && (
        <OdometerDialog
          vehicleId={id}
          currentKm={toNumber(current.currentOdometer) ?? 0}
          startCorrecting={odometerDialog === 'correct'}
          onClose={() => setOdometerDialog(null)}
        />
      )}
    </div>
  )
}
