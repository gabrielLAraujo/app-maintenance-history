import { useQuery } from '@tanstack/react-query'
import { api } from '../client'
import { unwrap } from '../unwrap'

/** Chave do cache dos lembretes vencidos e próximos (todos os veículos). Quem mexe em km ou em lembrete a invalida. */
export const dueRemindersKey = ['reminders', 'due'] as const

/** Lembretes vencidos e próximos de todos os veículos do usuário (a lista "Atenção" do painel). */
export function useDueReminders() {
  return useQuery({
    queryKey: dueRemindersKey,
    queryFn: () => unwrap(api.GET('/api/reminders/due')),
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
