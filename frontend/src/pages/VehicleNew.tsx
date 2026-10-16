import { useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { useCreateVehicle } from '../api/hooks/vehicles'
import type { Problem } from '../api/problem'
import type { components } from '../api/schema'
import { problemOf } from '../api/unwrap'
import { Alert } from '../components/Alert'
import { Button } from '../components/Button'
import { Card } from '../components/Card'
import { PageHeader } from '../components/PageHeader'
import { FipePicker, type FipePick } from './vehicle/FipePicker'
import { VehicleForm, type VehicleFormData, type VehicleFormValues } from './vehicle/VehicleForm'

type Catalog = components['schemas']['CatalogRefInput']

/** O que o formulário recebe ao sair do assistente: valores sugeridos e o vínculo com a FIPE (nulo no modo manual). */
type Draft = {
  /** Um novo rascunho recria o formulário do zero (os campos pegam os valores novos). */
  id: number
  values: Partial<VehicleFormValues>
  catalog: Catalog | null
  fipeUnavailable: boolean
  summary?: string
}

/** Cadastro: primeiro o assistente da FIPE (ou o modo manual), depois o formulário para conferir e completar. */
export function VehicleNew() {
  const navigate = useNavigate()
  const create = useCreateVehicle()
  const [draft, setDraft] = useState<Draft | null>(null)
  const drafts = useRef(0)

  function startFromFipe(pick: FipePick) {
    // A FIPE só informa o ano-modelo; o de fabricação começa igual e o usuário ajusta.
    const year = pick.modelYear === null ? '' : String(pick.modelYear)

    setDraft({
      id: ++drafts.current,
      values: {
        type: pick.catalog.type,
        make: pick.make,
        model: pick.model,
        version: pick.version,
        manufactureYear: year,
        modelYear: year,
        fuel: pick.fuel,
      },
      catalog: pick.catalog,
      fipeUnavailable: false,
      summary: [pick.make, pick.model, pick.version, pick.modelYear].filter(Boolean).join(' '),
    })
  }

  function startManual(reason?: 'unavailable') {
    setDraft({ id: ++drafts.current, values: {}, catalog: null, fipeUnavailable: reason === 'unavailable' })
  }

  async function submit(data: VehicleFormData): Promise<Problem | null> {
    if (data.currentOdometer === undefined) return { status: 400, detail: 'Informe a km atual.' }

    try {
      const { id } = await create.mutateAsync({ ...data, currentOdometer: data.currentOdometer, catalog: draft?.catalog ?? null })
      navigate(`/vehicles/${id}`)
      return null
    } catch (error) {
      return problemOf(error)
    }
  }

  return (
    <div>
      <PageHeader title="Novo veículo" back={{ to: '/', label: 'Painel' }} />

      {!draft ? (
        <Card>
          <FipePicker onPick={startFromFipe} onManual={startManual} />
        </Card>
      ) : (
        <div className="space-y-4">
          {draft.catalog && (
            <div className="flex items-center justify-between gap-3 rounded-lg bg-plate-soft px-4 py-3 text-sm text-plate ring-1 ring-inset ring-plate-line">
              <p className="min-w-0">
                <span className="block font-medium">Dados da FIPE</span>
                <span className="block truncate">{draft.summary}</span>
              </p>
              <Button variant="secondary" className="shrink-0 whitespace-nowrap" onClick={() => setDraft(null)}>
                Escolher outro
              </Button>
            </div>
          )}
          {draft.fipeUnavailable && (
            <Alert variant="warning" className="space-y-2">
              <p>A tabela FIPE não respondeu agora. Preencha os dados à mão; ninguém precisa dela para cadastrar.</p>
              <Button variant="secondary" onClick={() => setDraft(null)}>
                Tentar a FIPE de novo
              </Button>
            </Alert>
          )}
          {!draft.catalog && !draft.fipeUnavailable && (
            <p className="text-sm text-ink-2">
              Preenchimento manual.{' '}
              <button type="button" onClick={() => setDraft(null)} className="min-h-11 font-medium underline">
                Buscar na FIPE
              </button>
            </p>
          )}
          <VehicleForm
            key={draft.id}
            mode="create"
            initial={draft.values}
            lockType={draft.catalog !== null}
            submitLabel="Cadastrar veículo"
            onSubmit={submit}
            cancelTo="/"
          />
        </div>
      )}
    </div>
  )
}
