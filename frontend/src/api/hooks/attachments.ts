import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useCallback, useState } from 'react'
import { api } from '../client'
import { problemMessage } from '../problem'
import type { components } from '../schema'
import { problemOf, unwrap, unwrapEmpty } from '../unwrap'
import { recordKeys } from './records'

type AttachmentKind = components['schemas']['AttachmentKind']

/** Os limites do back-end (A1 e A3), aplicados aqui antes de enviar para não gastar o envio à toa. */
export const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024
export const MAX_ATTACHMENTS_PER_RECORD = 20

/**
 * Endereço do arquivo de um anexo. Vai direto no `src` de uma imagem ou num link: o navegador manda o cookie de sessão
 * (mesma origem), e a API confere o dono a cada pedido.
 */
export const attachmentContentUrl = (vehicleId: string, attachmentId: string) =>
  `/api/vehicles/${vehicleId}/attachments/${attachmentId}/content`

// O anexo muda o registro aberto (a galeria) e o contador de anexos da lista do histórico.
function useRefreshRecord(vehicleId: string, recordId: string) {
  const queryClient = useQueryClient()

  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: recordKeys.one(vehicleId, recordId) }),
      queryClient.invalidateQueries({ queryKey: recordKeys.lists(vehicleId) }),
    ])
}

function useUploadAttachment(vehicleId: string, recordId: string) {
  const refresh = useRefreshRecord(vehicleId, recordId)

  return useMutation({
    mutationFn: ({ file, kind }: { file: File; kind: AttachmentKind }) =>
      unwrap(
        api.POST('/api/vehicles/{vehicleId}/records/{recordId}/attachments', {
          params: { path: { vehicleId, recordId } },
          // O tipo gerado descreve o arquivo como texto (IFormFile); o corpo real é o multipart montado abaixo. Sem
          // `Content-Type` à mão: o navegador acrescenta o `boundary`, e o cliente já manda o X-XSRF-TOKEN.
          body: { file: '', kind },
          bodySerializer: () => {
            const form = new FormData()
            form.append('file', file)
            form.append('kind', kind)
            return form
          },
        }),
      ),
    onSuccess: refresh,
  })
}

export function useSetAttachmentVisibility(vehicleId: string, recordId: string) {
  const refresh = useRefreshRecord(vehicleId, recordId)

  return useMutation({
    mutationFn: ({ attachmentId, isPublic }: { attachmentId: string; isPublic: boolean }) =>
      unwrapEmpty(
        api.PATCH('/api/vehicles/{vehicleId}/attachments/{attachmentId}', {
          params: { path: { vehicleId, attachmentId } },
          body: { isPublic },
        }),
      ),
    onSuccess: refresh,
  })
}

export function useDeleteAttachment(vehicleId: string, recordId: string) {
  const refresh = useRefreshRecord(vehicleId, recordId)

  return useMutation({
    mutationFn: (attachmentId: string) =>
      unwrapEmpty(
        api.DELETE('/api/vehicles/{vehicleId}/attachments/{attachmentId}', { params: { path: { vehicleId, attachmentId } } }),
      ),
    onSuccess: refresh,
  })
}

export type UploadFailure = { fileName: string; message: string }

/** O que aconteceu com um lote de arquivos: quantos foram e, de cada um que não foi, o motivo. */
export type UploadReport = { uploaded: number; failures: UploadFailure[] }

export type UploadProgress = {
  /** Posição do arquivo em envio (1 = o primeiro). */
  current: number
  total: number
  fileName: string
}

// Depois destes erros os próximos arquivos falhariam do mesmo jeito (limite por minuto, limite por registro, sem
// rede, sessão expirada): o lote para em vez de insistir.
const stopsTheBatch = ({ status, code }: { status: number; code?: string }) =>
  status === 0 || status === 401 || status === 429 || code === 'attachment.limit_reached'

/**
 * Envio de anexos de um registro. Os arquivos vão **um por vez** (nunca em paralelo: o back-end limita os envios por
 * minuto e o celular em 4G não ganha nada com várias conexões). Arquivo acima de 10 MB e o que passaria de 20 anexos
 * no registro nem são enviados.
 */
export function useUploadAttachments(vehicleId: string, recordId: string) {
  const { mutateAsync } = useUploadAttachment(vehicleId, recordId)
  const [progress, setProgress] = useState<UploadProgress | null>(null)

  const upload = useCallback(
    async (files: File[], kind: AttachmentKind, alreadyAttached: number): Promise<UploadReport> => {
      const failures: UploadFailure[] = []
      const queue: File[] = []
      let room = MAX_ATTACHMENTS_PER_RECORD - alreadyAttached

      for (const file of files) {
        if (file.size > MAX_ATTACHMENT_BYTES) {
          failures.push({ fileName: file.name, message: 'Arquivo maior que 10 MB; não foi enviado.' })
        } else if (room <= 0) {
          failures.push({ fileName: file.name, message: 'O registro já está no limite de 20 anexos; não foi enviado.' })
        } else {
          queue.push(file)
          room -= 1
        }
      }

      let uploaded = 0
      try {
        for (const [index, file] of queue.entries()) {
          setProgress({ current: index + 1, total: queue.length, fileName: file.name })

          try {
            await mutateAsync({ file, kind })
            uploaded += 1
          } catch (error) {
            const problem = problemOf(error)
            failures.push({ fileName: file.name, message: problemMessage(problem) })

            if (stopsTheBatch(problem)) {
              for (const skipped of queue.slice(index + 1)) {
                failures.push({ fileName: skipped.name, message: 'Não foi enviado: o envio parou antes.' })
              }
              break
            }
          }
        }
      } finally {
        setProgress(null)
      }

      return { uploaded, failures }
    },
    [mutateAsync],
  )

  return { upload, progress }
}
