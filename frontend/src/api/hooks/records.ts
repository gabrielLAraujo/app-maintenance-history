import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../client'
import type { components } from '../schema'
import { isConflict, problemOf, unwrap, unwrapEmpty } from '../unwrap'
import { invalidateVehicle, vehicleKeys } from './vehicles'

type Schemas = components['schemas']

export type RecordFilters = {
  type?: Schemas['RecordType']
  category?: Schemas['Category']
}

/** Chaves do cache. Todas sob `['vehicle', id]`: invalidar o veículo atualiza o histórico e os registros abertos. */
export const recordKeys = {
  /** Todas as listas do histórico do veículo, com qualquer filtro. */
  lists: (vehicleId: string) => [...vehicleKeys.one(vehicleId), 'records'] as const,
  list: (vehicleId: string, filters: RecordFilters) => [...vehicleKeys.one(vehicleId), 'records', filters] as const,
  one: (vehicleId: string, recordId: string) => [...vehicleKeys.one(vehicleId), 'record', recordId] as const,
}

/** Histórico do veículo (mais recente primeiro), uma página por vez: `fetchNextPage` segue o `nextCursor`. */
export function useRecords(vehicleId: string, filters: RecordFilters) {
  return useInfiniteQuery({
    queryKey: recordKeys.list(vehicleId, filters),
    queryFn: ({ pageParam }) =>
      unwrap(
        api.GET('/api/vehicles/{vehicleId}/records', {
          params: { path: { vehicleId }, query: { type: filters.type, category: filters.category, cursor: pageParam } },
        }),
      ),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    // Trocar de filtro mantém a lista anterior na tela até a nova chegar (a tela não pisca nem perde o foco).
    placeholderData: keepPreviousData,
  })
}

/** Um registro completo (peças, anexos e `rowVersion`, para editar). */
export function useRecord(vehicleId: string, recordId: string) {
  return useQuery({
    queryKey: recordKeys.one(vehicleId, recordId),
    queryFn: () =>
      unwrap(api.GET('/api/vehicles/{vehicleId}/records/{recordId}', { params: { path: { vehicleId, recordId } } })),
  })
}

/** Edição concorrente (409): o registro em tela está velho; busca de novo para mostrar o que há no servidor. */
const reloadOnConflict = (queryClient: ReturnType<typeof useQueryClient>, vehicleId: string, recordId: string, error: unknown) => {
  if (isConflict(problemOf(error))) void queryClient.invalidateQueries({ queryKey: recordKeys.one(vehicleId, recordId) })
}

// Registro novo ou editado pode subir a km do veículo e (só o novo) cumprir lembretes: o veículo, o painel e os
// lembretes ficam velhos junto com o histórico.

export function useAddRecord(vehicleId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: Schemas['AddRecordRequest']) =>
      unwrap(api.POST('/api/vehicles/{vehicleId}/records', { params: { path: { vehicleId } }, body })),
    onSuccess: () => invalidateVehicle(queryClient, vehicleId),
  })
}

export function useUpdateRecord(vehicleId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ recordId, body }: { recordId: string; body: Schemas['UpdateRecordRequest'] }) =>
      unwrapEmpty(
        api.PUT('/api/vehicles/{vehicleId}/records/{recordId}', { params: { path: { vehicleId, recordId } }, body }),
      ),
    onSuccess: () => invalidateVehicle(queryClient, vehicleId),
    onError: (error, { recordId }) => reloadOnConflict(queryClient, vehicleId, recordId, error),
  })
}

export function useDeleteRecord(vehicleId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (recordId: string) =>
      unwrapEmpty(api.DELETE('/api/vehicles/{vehicleId}/records/{recordId}', { params: { path: { vehicleId, recordId } } })),
    onSuccess: (_, recordId) => {
      // O registro deixou de existir: sai do cache em vez de ser buscado de novo e dar 404. Excluir não mexe na km
      // do veículo (V4) nem nos lembretes (R6): buscam de novo o histórico e os gastos (do veículo, da lista de
      // veículos e do painel, cuja chave vive sob `vehicleKeys.all`).
      queryClient.removeQueries({ queryKey: recordKeys.one(vehicleId, recordId) })
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: recordKeys.lists(vehicleId) }),
        queryClient.invalidateQueries({ queryKey: [...vehicleKeys.one(vehicleId), 'summary'] }),
        queryClient.invalidateQueries({ queryKey: vehicleKeys.all }),
      ])
    },
  })
}
