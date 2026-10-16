import type { UseQueryResult } from '@tanstack/react-query'
import { useEffect, useEffectEvent, useRef, useState } from 'react'
import { useCatalogDetails, useCatalogMakes, useCatalogModels, useCatalogYears } from '../../api/hooks/catalog'
import type { components } from '../../api/schema'
import { errorMessage, isUnavailable, problemOf } from '../../api/unwrap'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Field } from '../../components/Field'
import { Spinner } from '../../components/Spinner'
import { toNumber } from '../../lib/format'
import { vehicleTypeLabels } from '../../lib/labels'

type Schemas = components['schemas']
type VehicleType = Schemas['VehicleType']
type Make = Schemas['CatalogMake']
type Model = Schemas['CatalogModel']
type Year = Schemas['CatalogYear']

/** O que a escolha na FIPE entrega ao formulário: tudo editável, mais o vínculo com o catálogo. */
export type FipePick = {
  make: string
  model: string
  version: string
  /** Nulo nos zero km da FIPE, que não trazem ano: o formulário pede ao usuário. */
  modelYear: number | null
  fuel: Schemas['Fuel']
  catalog: { type: VehicleType; fipeCode: string; fipeYearCode: string }
}

type Props = {
  onPick: (pick: FipePick) => void
  /** Sem motivo: o usuário preferiu preencher na mão. `'unavailable'`: a FIPE não respondeu (503), a tela avisa. */
  onManual: (reason?: 'unavailable') => void
}

/** As escolhas feitas até aqui; cada passo só existe depois do anterior. */
type Choice = { type?: VehicleType; make?: Make; model?: Model }

const TYPES: VehicleType[] = ['car', 'motorcycle', 'truck']

// Marcas como a Chevrolet têm mais de mil modelos: desenhar tudo travaria o celular. Quem quer um específico digita.
const MAX_ITEMS = 100

// Sem maiúsculas e sem acento: "volks" acha "VW - VolksWagen"; "hibrido" acha "Híbrido".
const fold = (text: string) => text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase()

function matches(label: string, search: string): boolean {
  const haystack = fold(label)
  return fold(search)
    .split(/\s+/)
    .filter(Boolean)
    .every((term) => haystack.includes(term))
}

type ListProps<T> = {
  query: Pick<UseQueryResult<T[], Error>, 'data' | 'error' | 'isPending' | 'refetch'>
  getKey: (item: T) => string
  getLabel: (item: T) => string
  onSelect: (item: T) => void
  onUnavailable: () => void
  searchLabel: string
  loadingLabel: string
  emptyText: string
}

/** Lista de uma etapa do catálogo, com busca por texto. 503 chama `onUnavailable`; outro erro oferece tentar de novo. */
function CatalogList<T>({
  query,
  getKey,
  getLabel,
  onSelect,
  onUnavailable,
  searchLabel,
  loadingLabel,
  emptyText,
}: ListProps<T>) {
  const [search, setSearch] = useState('')
  const notifyUnavailable = useEffectEvent(onUnavailable)
  const { data, error, isPending, refetch } = query

  useEffect(() => {
    if (error && isUnavailable(problemOf(error))) notifyUnavailable()
  }, [error])

  if (isPending) return <Loading>{loadingLabel}</Loading>

  if (error || !data) {
    return (
      <div className="space-y-3">
        <Alert variant="error">{errorMessage(error)}</Alert>
        <Button variant="secondary" onClick={() => void refetch()}>
          Tentar de novo
        </Button>
      </div>
    )
  }

  const found = data.filter((item) => matches(getLabel(item), search))
  const shown = found.slice(0, MAX_ITEMS)

  return (
    <div className="space-y-3">
      <Field
        label={searchLabel}
        type="search"
        autoComplete="off"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
      />
      {found.length === 0 ? (
        <p className="py-4 text-center text-ink-2">{emptyText}</p>
      ) : (
        <ul className="divide-y divide-track overflow-hidden rounded-lg ring-1 ring-line">
          {shown.map((item) => (
            <li key={getKey(item)}>
              <button
                type="button"
                onClick={() => onSelect(item)}
                className="block min-h-12 w-full bg-white px-4 py-3 text-left text-base hover:bg-amber-soft focus-visible:bg-amber-soft focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-amber-ink"
              >
                {getLabel(item)}
              </button>
            </li>
          ))}
        </ul>
      )}
      {found.length > shown.length && (
        <p className="text-center text-sm text-muted">
          Mostrando {shown.length} de {found.length}. Digite na busca para achar o seu.
        </p>
      )}
    </div>
  )
}

function Loading({ children }: { children: string }) {
  return (
    <p className="flex items-center gap-2 py-6 text-ink-2">
      <Spinner size="sm" label={null} /> {children}
    </p>
  )
}

/**
 * Assistente da FIPE: tipo → marca → modelo → ano. Ao escolher o ano, busca os detalhes (código FIPE, combustível) e
 * entrega tudo em `onPick`. Cadastrar veículo nunca depende da FIPE: "Não achei" está em todos os passos e a FIPE
 * fora do ar (503) abre o modo manual sozinha.
 */
export function FipePicker({ onPick, onManual }: Props) {
  const [choice, setChoice] = useState<Choice>({})
  const [loadingDetails, setLoadingDetails] = useState(false)
  const [detailsError, setDetailsError] = useState<string | null>(null)
  const loadDetails = useCatalogDetails()
  // Cada escolha de ano numera a busca dos detalhes; voltar, desistir ou sair da tela invalida a que estiver em andamento.
  const attempt = useRef(0)

  useEffect(
    () => () => {
      attempt.current += 1
    },
    [],
  )

  function goManual(reason?: 'unavailable') {
    attempt.current += 1
    // Sem motivo, onManual() é chamado sem argumento algum (não com undefined).
    if (reason) onManual(reason)
    else onManual()
  }

  function goBackTo(keep: Choice) {
    attempt.current += 1
    setLoadingDetails(false)
    setDetailsError(null)
    setChoice(keep)
  }

  async function pickYear(year: Year, type: VehicleType, make: Make, model: Model) {
    const mine = ++attempt.current
    setLoadingDetails(true)
    setDetailsError(null)

    try {
      const details = await loadDetails({ type, makeId: make.id, modelId: model.id, yearId: year.id })
      if (attempt.current !== mine) return

      onPick({
        make: details.make,
        model: model.suggestedModel,
        version: model.suggestedVersion,
        modelYear: toNumber(details.modelYear) ?? toNumber(year.modelYear),
        fuel: details.fuel,
        catalog: { type, fipeCode: details.fipeCode, fipeYearCode: year.id },
      })
    } catch (error) {
      if (attempt.current !== mine) return

      if (isUnavailable(problemOf(error))) goManual('unavailable')
      else setDetailsError(errorMessage(error))
      setLoadingDetails(false)
    }
  }

  const { type, make, model } = choice
  const unavailable = () => goManual('unavailable')

  function renderStep() {
    if (loadingDetails) return <Loading>Buscando os dados do veículo…</Loading>

    if (!type) {
      return (
        <div className="grid gap-3 sm:grid-cols-3">
          {TYPES.map((value) => (
            <Button key={value} variant="secondary" className="min-h-16" onClick={() => setChoice({ type: value })}>
              {vehicleTypeLabels[value]}
            </Button>
          ))}
        </div>
      )
    }
    if (!make) return <MakeStep type={type} onSelect={(next) => setChoice({ type, make: next })} onUnavailable={unavailable} />
    if (!model) {
      return (
        <ModelStep
          type={type}
          make={make}
          onSelect={(next) => setChoice({ type, make, model: next })}
          onUnavailable={unavailable}
        />
      )
    }
    return (
      <YearStep
        type={type}
        make={make}
        model={model}
        onSelect={(year) => void pickYear(year, type, make, model)}
        onUnavailable={unavailable}
      />
    )
  }

  const titles = ['Que tipo de veículo?', 'Escolha a marca', 'Escolha o modelo', 'Escolha o ano']
  const step = !type ? 0 : !make ? 1 : !model ? 2 : 3
  // O caminho já escolhido: tocar num item volta àquele passo, descartando o que veio depois.
  const path: { label: string; goBack: Choice }[] = []
  if (type) path.push({ label: vehicleTypeLabels[type], goBack: {} })
  if (make) path.push({ label: make.name, goBack: { type } })
  if (model) path.push({ label: model.fullName, goBack: { type, make } })

  return (
    <div className="space-y-4">
      <div>
        <p className="text-sm text-muted">Passo {step + 1} de 4</p>
        <h2 className="text-lg font-semibold">{titles[step]}</h2>
      </div>

      {path.length > 0 && (
        <nav aria-label="Escolhas até aqui">
          <ol className="flex flex-wrap gap-2">
            {path.map((item) => (
              <li key={item.label}>
                <button
                  type="button"
                  onClick={() => goBackTo(item.goBack)}
                  className="min-h-11 rounded-full bg-track px-3 text-left text-sm text-ink-2 hover:bg-line"
                >
                  {item.label} <span className="text-muted underline">alterar</span>
                </button>
              </li>
            ))}
          </ol>
        </nav>
      )}

      {detailsError && <Alert variant="error">{detailsError}</Alert>}

      {renderStep()}

      <Button variant="secondary" className="w-full" onClick={() => goManual()}>
        Não achei, preencher manualmente
      </Button>
    </div>
  )
}

type StepProps<T> = { onSelect: (item: T) => void; onUnavailable: () => void }

function MakeStep({ type, onSelect, onUnavailable }: StepProps<Make> & { type: VehicleType }) {
  const query = useCatalogMakes(type)

  return (
    <CatalogList
      query={query}
      getKey={(item) => item.id}
      getLabel={(item) => item.name}
      onSelect={onSelect}
      onUnavailable={onUnavailable}
      searchLabel="Buscar marca"
      loadingLabel="Carregando marcas…"
      emptyText="Nenhuma marca encontrada."
    />
  )
}

function ModelStep({ type, make, onSelect, onUnavailable }: StepProps<Model> & { type: VehicleType; make: Make }) {
  const query = useCatalogModels(type, make.id)

  return (
    <CatalogList
      query={query}
      getKey={(item) => item.id}
      getLabel={(item) => item.fullName}
      onSelect={onSelect}
      onUnavailable={onUnavailable}
      searchLabel="Buscar modelo"
      loadingLabel="Carregando modelos…"
      emptyText="Nenhum modelo encontrado."
    />
  )
}

function YearStep({
  type,
  make,
  model,
  onSelect,
  onUnavailable,
}: StepProps<Year> & { type: VehicleType; make: Make; model: Model }) {
  const query = useCatalogYears(type, make.id, model.id)

  return (
    <CatalogList
      query={query}
      getKey={(item) => item.id}
      getLabel={(item) => item.label}
      onSelect={onSelect}
      onUnavailable={onUnavailable}
      searchLabel="Buscar ano"
      loadingLabel="Carregando anos…"
      emptyText="Nenhum ano encontrado."
    />
  )
}
