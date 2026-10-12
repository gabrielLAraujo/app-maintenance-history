import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../client'
import { unwrap } from '../unwrap'
import { invalidateVehicleLists } from './vehicles'

/**
 * Carrega os veículos de exemplo. Termina só depois de buscar de novo os veículos e os lembretes: quando o botão
 * volta ao normal, o painel já está cheio. Se a conta já tinha veículos (409), a lista também está velha.
 */
export function useSeedDemo() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => unwrap(api.POST('/api/demo/seed')),
    onSettled: () => invalidateVehicleLists(queryClient),
  })
}
