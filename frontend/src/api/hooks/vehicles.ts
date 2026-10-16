import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api } from '../client'
import type { components } from '../schema'
import { isConflict, problemOf, unwrap, unwrapEmpty } from '../unwrap'
import { allRemindersKey, dueRemindersKey } from './reminders'

type Schemas = components['schemas']

/**
 * Chaves do cache. `['vehicle', id]` é prefixo de tudo que pertence a um veículo (registros, lembretes, links):
 * invalidar a chave de `one(id)` atualiza o veículo e os dados dele de uma vez.
 */
export const vehicleKeys = {
  all: ['vehicles'] as const,
  one: (id: string) => ['vehicle', id] as const,
}

// Os dois veículos de exemplo (e qualquer lista) podem chegar em qualquer ordem: a tela mostra por nome.
const byDisplayName = (vehicles: Schemas['VehicleSummaryDto'][]) =>
  [...vehicles].sort((a, b) => a.displayName.localeCompare(b.displayName, 'pt-BR'))

/** Veículos do usuário, em ordem alfabética do nome de exibição. */
export function useVehicles() {
  return useQuery({
    queryKey: vehicleKeys.all,
    queryFn: () => unwrap(api.GET('/api/vehicles')),
    select: byDisplayName,
  })
}

/** Um veículo completo (com `rowVersion`, para editar). */
export function useVehicle(id: string) {
  return useQuery({
    queryKey: vehicleKeys.one(id),
    queryFn: () => unwrap(api.GET('/api/vehicles/{vehicleId}', { params: { path: { vehicleId: id } } })),
  })
}

/**
 * Valor de tabela da FIPE de um veículo vinculado. `enabled` só para quem tem `catalog` (senão a API responde 404
 * `vehicle.catalog_not_linked`). Sem `retry`: a API já tenta a FIPE mais de uma vez antes de responder 503.
 */
export function useFipePrice(id: string, enabled: boolean) {
  return useQuery({
    queryKey: [...vehicleKeys.one(id), 'fipe-price'] as const,
    queryFn: () => unwrap(api.GET('/api/vehicles/{vehicleId}/fipe-price', { params: { path: { vehicleId: id } } })),
    enabled,
    staleTime: 30 * 60_000,
    retry: false,
  })
}

/** A lista do painel e os lembretes dependem dos veículos (nome, km): mexeu num veículo, os dois ficam velhos. */
export const invalidateVehicleLists = (queryClient: QueryClient) =>
  Promise.all([
    queryClient.invalidateQueries({ queryKey: vehicleKeys.all }),
    queryClient.invalidateQueries({ queryKey: dueRemindersKey }),
    queryClient.invalidateQueries({ queryKey: allRemindersKey }),
  ])

/** O veículo e tudo que é dele (registros, lembretes, valor FIPE), mais a lista do painel e os lembretes vencidos. */
export const invalidateVehicle = (queryClient: QueryClient, id: string) =>
  Promise.all([queryClient.invalidateQueries({ queryKey: vehicleKeys.one(id) }), invalidateVehicleLists(queryClient)])

/** Edição concorrente (409): o dado em tela está velho; busca de novo para a tela mostrar o que há no servidor. */
const reloadOnConflict = (queryClient: QueryClient, id: string, error: unknown) => {
  if (isConflict(problemOf(error))) void queryClient.invalidateQueries({ queryKey: vehicleKeys.one(id) })
}

export function useCreateVehicle() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: Schemas['CreateVehicleRequest']) => unwrap(api.POST('/api/vehicles', { body })),
    onSuccess: () => invalidateVehicleLists(queryClient),
  })
}

export function useUpdateVehicle() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: Schemas['UpdateVehicleRequest'] }) =>
      unwrapEmpty(api.PUT('/api/vehicles/{vehicleId}', { params: { path: { vehicleId: id } }, body })),
    onSuccess: (_, { id }) => invalidateVehicle(queryClient, id),
    onError: (error, { id }) => reloadOnConflict(queryClient, id, error),
  })
}

export function useDeleteVehicle() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) =>
      unwrapEmpty(api.DELETE('/api/vehicles/{vehicleId}', { params: { path: { vehicleId: id } } })),
    onSuccess: (_, id) => {
      // O veículo (e o que era dele) deixou de existir: sai do cache em vez de ser buscado de novo e dar 404.
      queryClient.removeQueries({ queryKey: vehicleKeys.one(id) })
      return invalidateVehicleLists(queryClient)
    },
  })
}

/** Atualização da km (só aumenta; diminuir dá 422 `vehicle.odometer_cannot_decrease`). */
export function useUpdateOdometer() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, km }: { id: string; km: number }) =>
      unwrapEmpty(api.POST('/api/vehicles/{vehicleId}/odometer', { params: { path: { vehicleId: id } }, body: { km } })),
    // A km muda o status dos lembretes (próximo/vencido): o painel precisa buscar de novo.
    onSuccess: (_, { id }) => invalidateVehicle(queryClient, id),
    onError: (error, { id }) => reloadOnConflict(queryClient, id, error),
  })
}

/** Correção de digitação da km (pode diminuir, mas não abaixo da maior km do histórico: 422 `vehicle.odometer_below_records`). */
export function useCorrectOdometer() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, km }: { id: string; km: number }) =>
      unwrapEmpty(
        api.POST('/api/vehicles/{vehicleId}/odometer/correction', { params: { path: { vehicleId: id } }, body: { km } }),
      ),
    onSuccess: (_, { id }) => invalidateVehicle(queryClient, id),
    onError: (error, { id }) => reloadOnConflict(queryClient, id, error),
  })
}

/**
 * Define (ou, com `null`, remove) a capa do veículo: uma foto de um registro dele. A capa aparece no veículo, nos
 * cards da lista e do painel.
 */
export function useSetCoverPhoto(vehicleId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (attachmentId: string | null) =>
      unwrapEmpty(
        api.PUT('/api/vehicles/{vehicleId}/cover-photo', { params: { path: { vehicleId } }, body: { attachmentId } }),
      ),
    onSuccess: () => invalidateVehicle(queryClient, vehicleId),
  })
}
