import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api } from '../client'
import type { components } from '../schema'
import { isConflict, problemOf, unwrap, unwrapEmpty } from '../unwrap'
import { vehicleKeys } from './vehicles'

type Schemas = components['schemas']

/** Chave dos links públicos de um veículo, sob `['vehicle', id]` como o resto do que pertence a ele. */
export const shareLinksKey = (vehicleId: string) => [...vehicleKeys.one(vehicleId), 'share-links'] as const

/** Links públicos do veículo (ativos, revogados e expirados), com o status já calculado pela API (`isActive`). */
export function useShareLinks(vehicleId: string) {
  return useQuery({
    queryKey: shareLinksKey(vehicleId),
    queryFn: () => unwrap(api.GET('/api/vehicles/{vehicleId}/share-links', { params: { path: { vehicleId } } })),
  })
}

export function useCreateShareLink(vehicleId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: Schemas['CreateShareLinkRequest']) =>
      unwrap(api.POST('/api/vehicles/{vehicleId}/share-links', { params: { path: { vehicleId } }, body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: shareLinksKey(vehicleId) }),
  })
}

/** A tela mostra um link que já mudou (revogado em outra tela, ou que nem existe mais): busca a lista de novo. */
const reloadWhenStale = (queryClient: QueryClient, vehicleId: string, error: unknown) => {
  const problem = problemOf(error)
  if (isConflict(problem) || problem.status === 404) void queryClient.invalidateQueries({ queryKey: shareLinksKey(vehicleId) })
}

/** Revogar derruba o link na hora: quem o recebeu passa a ver "Link não encontrado ou expirado". Não desfaz. */
export function useRevokeShareLink(vehicleId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (linkId: string) =>
      unwrapEmpty(api.DELETE('/api/vehicles/{vehicleId}/share-links/{linkId}', { params: { path: { vehicleId, linkId } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: shareLinksKey(vehicleId) }),
    onError: (error) => reloadWhenStale(queryClient, vehicleId, error),
  })
}
