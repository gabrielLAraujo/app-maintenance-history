import { useInfiniteQuery } from '@tanstack/react-query'
import { api } from '../client'
import { unwrap } from '../unwrap'

/** Chave do cache da visão pública de um link. Fora de `['vehicle', id]`: quem vê o link não é o dono e não sabe o id. */
export const publicVehicleKey = (token: string) => ['public', token] as const

/**
 * A visão pública de um link, uma página de registros por vez (`fetchNextPage` segue o `nextCursor`). É anônima:
 * `credentials: 'omit'` não manda o cookie de sessão (a página funciona igual com ou sem conta) e a rota nunca dispara
 * o 401 do cliente. Link inexistente, revogado, expirado e malformado dão a mesma resposta (404).
 */
export function usePublicVehicle(token: string) {
  return useInfiniteQuery({
    queryKey: publicVehicleKey(token),
    queryFn: ({ pageParam }) =>
      unwrap(
        api.GET('/api/public/{token}', {
          params: { path: { token }, query: { cursor: pageParam } },
          credentials: 'omit',
        }),
      ),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  })
}

/** Endereço de uma foto pública, para o `src` de uma imagem ou um link (só fotos marcadas como públicas respondem). */
export const publicAttachmentUrl = (token: string, attachmentId: string) =>
  `/api/public/${encodeURIComponent(token)}/attachments/${encodeURIComponent(attachmentId)}`
