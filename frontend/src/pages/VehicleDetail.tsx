import { useState } from 'react'
import { useParams, useSearchParams } from 'react-router'
import { useFipePrice, useVehicle } from '../api/hooks/vehicles'
import { Button } from '../components/Button'
import { ButtonLink } from '../components/ButtonLink'
import { Card } from '../components/Card'
import { Km } from '../components/Km'
import { Money } from '../components/Money'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { Tabs } from '../components/Tabs'
import { formatInstantDate, toNumber } from '../lib/format'
import { fuelLabels } from '../lib/labels'
import { HistoryTab } from './vehicle/HistoryTab'
import { OdometerDialog } from './vehicle/OdometerDialog'
import { VehicleLoadError } from './vehicle/VehicleLoadError'

const TABS = [
  { id: 'history', label: 'Histórico' },
  { id: 'reminders', label: 'Lembretes' },
  { id: 'share', label: 'Compartilhar' },
] as const

type TabId = (typeof TABS)[number]['id']

/** Tela do veículo: cabeçalho com a km (e o valor FIPE, se vinculado) e as abas Histórico, Lembretes e Compartilhar. */
export function VehicleDetail() {
  const { id = '' } = useParams()
  const vehicle = useVehicle(id)
  const [params, setParams] = useSearchParams()
  const [odometerDialog, setOdometerDialog] = useState<'update' | 'correct' | null>(null)
  const linked = vehicle.data?.catalog != null
  const fipe = useFipePrice(id, linked)

  if (vehicle.isPending) {
    return (
      <p className="flex items-center gap-2 py-8 text-slate-600">
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

  return (
    <div>
      <PageHeader
        title={current.nickname ?? fullName}
        back={{ to: '/', label: 'Painel' }}
        actions={
          <ButtonLink to={`/vehicles/${id}/edit`} variant="secondary">
            Editar
          </ButtonLink>
        }
      >
        <p className="mt-1 text-slate-600">
          {[current.nickname && fullName, current.version, fuelLabels[current.fuel]].filter(Boolean).join(' · ')}
        </p>
        {current.plate && (
          <span className="mt-2 inline-block rounded-md border border-slate-300 bg-white px-2 py-0.5 font-mono text-sm font-semibold tracking-wider">
            {current.plate}
          </span>
        )}
      </PageHeader>

      <Card>
        <div className="flex items-center justify-between gap-3">
          <div>
            <p className="text-sm text-slate-500">Quilometragem</p>
            <Km value={current.currentOdometer} className="block text-2xl font-bold" />
            <p className="text-sm text-slate-500">atualizada em {formatInstantDate(current.odometerUpdatedAt)}</p>
          </div>
          <Button onClick={() => setOdometerDialog('update')}>Atualizar km</Button>
        </div>
        <button
          type="button"
          onClick={() => setOdometerDialog('correct')}
          className="mt-1 -ml-1 min-h-11 px-1 text-sm font-medium text-slate-700 underline hover:text-slate-900"
        >
          Corrigir km digitada errado
        </button>

        {linked && (
          <div className="mt-2 flex items-center justify-between gap-3 border-t border-slate-200 pt-3">
            <div>
              <p className="text-sm text-slate-500">Tabela FIPE</p>
              {fipe.isPending ? (
                <p className="text-sm text-slate-500">Consultando…</p>
              ) : fipe.data ? (
                <>
                  <Money value={fipe.data.price} className="block text-xl font-bold" />
                  <p className="text-sm text-slate-500">referência {fipe.data.referenceMonth}</p>
                </>
              ) : (
                // 503 (FIPE fora do ar), limite de consultas ou qualquer outra falha: o valor é um extra, nunca trava a tela.
                <p className="text-sm text-slate-500">Valor indisponível agora.</p>
              )}
            </div>
            {fipe.isError && (
              <Button variant="secondary" onClick={() => void fipe.refetch()}>
                Tentar de novo
              </Button>
            )}
          </div>
        )}
      </Card>

      <div className="mt-6">
        <Tabs label="Seções do veículo" tabs={TABS} value={tab} onChange={selectTab}>
          {tab === 'history' && <HistoryTab key={id} vehicleId={id} />}
          {/* Provisório: a Task F5 troca estes dois painéis por RemindersTab e ShareTab. */}
          {tab === 'reminders' && <p className="py-4 text-center text-slate-500">Os lembretes deste veículo chegam em seguida.</p>}
          {tab === 'share' && <p className="py-4 text-center text-slate-500">O compartilhamento por link chega em seguida.</p>}
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
