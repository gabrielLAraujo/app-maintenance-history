import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback } from 'react'
import { api } from '../client'
import type { components } from '../schema'
import { unwrap } from '../unwrap'

type VehicleType = components['schemas']['VehicleType']

// O servidor já guarda marcas, modelos e anos por 30 dias e os detalhes por 7: pedir de novo na mesma sessão é desperdício.
const STALE_TIME = 30 * 60_000

// `retry: false` porque o servidor já tenta a FIPE mais de uma vez; repetir aqui só atrasaria o modo manual (503).

export function useCatalogMakes(type: VehicleType) {
  return useQuery({
    queryKey: ['catalog', type, 'makes'],
    queryFn: () => unwrap(api.GET('/api/catalog/{type}/makes', { params: { path: { type } } })),
    staleTime: STALE_TIME,
    retry: false,
  })
}

export function useCatalogModels(type: VehicleType, makeId: string) {
  return useQuery({
    queryKey: ['catalog', type, makeId, 'models'],
    queryFn: () =>
      unwrap(api.GET('/api/catalog/{type}/makes/{makeId}/models', { params: { path: { type, makeId } } })),
    staleTime: STALE_TIME,
    retry: false,
  })
}

export function useCatalogYears(type: VehicleType, makeId: string, modelId: string) {
  return useQuery({
    queryKey: ['catalog', type, makeId, modelId, 'years'],
    queryFn: () =>
      unwrap(
        api.GET('/api/catalog/{type}/makes/{makeId}/models/{modelId}/years', {
          params: { path: { type, makeId, modelId } },
        }),
      ),
    staleTime: STALE_TIME,
    retry: false,
  })
}

type DetailsArgs = { type: VehicleType; makeId: string; modelId: string; yearId: string }

/**
 * Os detalhes (código FIPE, combustível, preço) só interessam no instante em que o usuário escolhe o ano: devolve a
 * função que busca (com cache), em vez de uma consulta que ficaria esperando um ano escolhido.
 */
export function useCatalogDetails() {
  const queryClient = useQueryClient()

  return useCallback(
    ({ type, makeId, modelId, yearId }: DetailsArgs) =>
      queryClient.fetchQuery({
        queryKey: ['catalog', type, makeId, modelId, yearId, 'details'],
        queryFn: () =>
          unwrap(
            api.GET('/api/catalog/{type}/makes/{makeId}/models/{modelId}/years/{yearId}', {
              params: { path: { type, makeId, modelId, yearId } },
            }),
          ),
        staleTime: STALE_TIME,
      }),
    [queryClient],
  )
}
