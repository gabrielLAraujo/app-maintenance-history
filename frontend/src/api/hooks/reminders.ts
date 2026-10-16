import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api } from '../client'
import type { components } from '../schema'
import { isConflict, problemOf, unwrap, unwrapEmpty } from '../unwrap'

type Schemas = components['schemas']

/** Chave do cache dos lembretes vencidos e próximos (todos os veículos). Quem mexe em km ou em lembrete a invalida. */
export const dueRemindersKey = ['reminders', 'due'] as const

/** Lembretes vencidos e próximos de todos os veículos do usuário (a lista "Atenção" do painel). */
export function useDueReminders() {
  return useQuery({
    queryKey: dueRemindersKey,
    queryFn: () => unwrap(api.GET('/api/reminders/due')),
  })
}

/** Chave de todos os lembretes do usuário, inclusive os em dia (a página Lembretes). */
export const allRemindersKey = ['reminders', 'all'] as const

/** Todos os lembretes de todos os veículos, com o status, o pior estado primeiro. */
export function useAllReminders() {
  return useQuery({
    queryKey: allRemindersKey,
    queryFn: () => unwrap(api.GET('/api/reminders')),
  })
}

/**
 * Chave dos lembretes de um veículo. Começa por `['vehicle', id]` (a convenção de `vehicleKeys`, que não se importa
 * aqui para não fazer ciclo): invalidar o veículo atualiza também a lista.
 */
export const vehicleRemindersKey = (vehicleId: string) => ['vehicle', vehicleId, 'reminders'] as const

/** Lembretes de um veículo, com o status calculado. */
export function useVehicleReminders(vehicleId: string) {
  return useQuery({
    queryKey: vehicleRemindersKey(vehicleId),
    queryFn: () => unwrap(api.GET('/api/vehicles/{vehicleId}/reminders', { params: { path: { vehicleId } } })),
  })
}

/** Criar, editar ou excluir um lembrete muda a lista do veículo, o painel e a página Lembretes: todos ficam velhos. */
const invalidateReminders = (queryClient: QueryClient, vehicleId: string) =>
  Promise.all([
    queryClient.invalidateQueries({ queryKey: vehicleRemindersKey(vehicleId) }),
    queryClient.invalidateQueries({ queryKey: dueRemindersKey }),
    queryClient.invalidateQueries({ queryKey: allRemindersKey }),
  ])

/** O que a tela mostra está velho (editado em outra tela, ou já excluído): busca de novo, sem esperar o usuário. */
const reloadWhenStale = (queryClient: QueryClient, vehicleId: string, error: unknown) => {
  const problem = problemOf(error)
  if (isConflict(problem) || problem.status === 404) void invalidateReminders(queryClient, vehicleId)
}

export function useCreateReminder(vehicleId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: Schemas['CreateReminderRequest']) =>
      unwrap(api.POST('/api/vehicles/{vehicleId}/reminders', { params: { path: { vehicleId } }, body })),
    onSuccess: () => invalidateReminders(queryClient, vehicleId),
  })
}

export function useUpdateReminder(vehicleId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ reminderId, body }: { reminderId: string; body: Schemas['UpdateReminderRequest'] }) =>
      unwrapEmpty(
        api.PUT('/api/vehicles/{vehicleId}/reminders/{reminderId}', { params: { path: { vehicleId, reminderId } }, body }),
      ),
    onSuccess: () => invalidateReminders(queryClient, vehicleId),
    onError: (error) => reloadWhenStale(queryClient, vehicleId, error),
  })
}

export function useDeleteReminder(vehicleId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (reminderId: string) =>
      unwrapEmpty(api.DELETE('/api/vehicles/{vehicleId}/reminders/{reminderId}', { params: { path: { vehicleId, reminderId } } })),
    onSuccess: () => invalidateReminders(queryClient, vehicleId),
    onError: (error) => reloadWhenStale(queryClient, vehicleId, error),
  })
}
