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
      // A foto de capa que era deste registro saiu com ele (o banco limpa a capa): o cache do veículo para de apontar
      // para ela já, sem esperar a busca nova, para a página do veículo não mostrar uma imagem quebrada.
      const record = queryClient.getQueryData<Schemas['RecordDto']>(recordKeys.one(vehicleId, recordId))
      queryClient.setQueryData<Schemas['VehicleDto']>(vehicleKeys.one(vehicleId), (vehicle) =>
        vehicle?.coverPhotoId && record?.attachments.some((attachment) => attachment.id === vehicle.coverPhotoId)
          ? { ...vehicle, coverPhotoId: null }
          : vehicle,
      )
      // O registro deixou de existir: sai do cache em vez de ser buscado de novo e dar 404. O resto do veículo (histórico,
      // gastos, capa), as listas e o painel buscam de novo.
      queryClient.removeQueries({ queryKey: recordKeys.one(vehicleId, recordId) })
      // Sem esperar: a tela volta ao veículo na hora, mostrando o cache já sem a capa enquanto a busca nova acontece.
      void invalidateVehicle(queryClient, vehicleId)
    },
  })
}
