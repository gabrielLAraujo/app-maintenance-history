import { useQuery } from '@tanstack/react-query'
import { api } from '../client'
import { unwrap } from '../unwrap'
import { vehicleKeys } from './vehicles'

/**
 * Resumo da garagem (gasto do ano, meses, últimos serviços). A chave começa por `vehicleKeys.all`: tudo o que invalida a
 * lista de veículos (registro novo, km, exclusão) invalida também este resumo.
 */
export const garageSummaryKey = [...vehicleKeys.all, 'garage-summary'] as const

export function useGarageSummary() {
  return useQuery({
    queryKey: garageSummaryKey,
    queryFn: () => unwrap(api.GET('/api/garage/summary')),
  })
}

/** Gastos de um veículo. Vive sob `vehicleKeys.one(id)`: invalidar o veículo atualiza o resumo. */
export const vehicleSummaryKey = (vehicleId: string) => [...vehicleKeys.one(vehicleId), 'summary'] as const

export function useVehicleSummary(vehicleId: string) {
  return useQuery({
    queryKey: vehicleSummaryKey(vehicleId),
    queryFn: () => unwrap(api.GET('/api/vehicles/{vehicleId}/summary', { params: { path: { vehicleId } } })),
  })
}

/** A foto de capa é um anexo do veículo: o arquivo vem do endpoint de conteúdo que já existe (com a sessão). */
export const coverUrl = (vehicleId: string, attachmentId: string) =>
  `/api/vehicles/${vehicleId}/attachments/${attachmentId}/content`
