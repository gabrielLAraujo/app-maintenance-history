import { useRef, useState, type ChangeEvent } from 'react'
import {
  attachmentContentUrl,
  MAX_ATTACHMENTS_PER_RECORD,
  useDeleteAttachment,
  useSetAttachmentVisibility,
  useUploadAttachments,
  type UploadReport,
} from '../../api/hooks/attachments'
import type { components } from '../../api/schema'
import { errorMessage } from '../../api/unwrap'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Card } from '../../components/Card'
import { Modal } from '../../components/Modal'
import { OptionPill } from '../../components/OptionPill'
import { formatFileSize, toNumber } from '../../lib/format'

type Schemas = components['schemas']
type Attachment = Schemas['AttachmentDto']
type AttachmentKind = Schemas['AttachmentKind']

// Lista explícita (e não image/*): no iOS isso faz o Safari entregar JPEG em vez de HEIC, que a API não aceita.
const IMAGE_TYPES = 'image/jpeg,image/png,image/webp'

const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`

type Props = {
  vehicleId: string
  recordId: string
  attachments: Attachment[]
}

/** Galeria de anexos do registro (fotos em miniatura, documentos em lista) e o envio de novos. */
export function Attachments({ vehicleId, recordId, attachments }: Props) {
  const photos = attachments.filter((attachment) => attachment.kind === 'photo')
  const documents = attachments.filter((attachment) => attachment.kind === 'document')
  const [removing, setRemoving] = useState<Attachment | null>(null)

  return (
    <Card aria-labelledby="attachments-title">
      <div className="flex items-baseline justify-between gap-3">
        <h2 id="attachments-title" className="text-base font-semibold">
          Anexos
        </h2>
        <span className="text-sm text-slate-500">
          {attachments.length} de {MAX_ATTACHMENTS_PER_RECORD}
        </span>
      </div>

      {attachments.length === 0 && (
        <p className="mt-2 text-slate-600">Nenhum anexo ainda. Tire uma foto do serviço, da peça ou da nota fiscal.</p>
      )}

      {photos.length > 0 && (
        <ul className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3">
          {photos.map((photo) => (
            <PhotoTile key={photo.id} vehicleId={vehicleId} recordId={recordId} photo={photo} onRemove={setRemoving} />
          ))}
        </ul>
      )}

      {documents.length > 0 && (
        <ul className="mt-3 divide-y divide-slate-100">
          {documents.map((file) => (
            <DocumentRow key={file.id} vehicleId={vehicleId} file={file} onRemove={setRemoving} />
          ))}
        </ul>
      )}

      <UploadControls vehicleId={vehicleId} recordId={recordId} attached={attachments.length} />

      {removing && (
        <RemoveDialog vehicleId={vehicleId} recordId={recordId} attachment={removing} onClose={() => setRemoving(null)} />
      )}
    </Card>
  )
}

function TrashIcon() {
  return (
    <svg viewBox="0 0 24 24" className="size-5" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M3 6h18M8 6V4h8v2M6 6l1 14h10l1-14M10 10v6M14 10v6" />
    </svg>
  )
}

function FileIcon() {
  return (
    <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-slate-600" aria-hidden="true">
      <svg viewBox="0 0 24 24" className="size-5" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
        <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z" />
        <path d="M14 3v5h5M9 13h6M9 17h6" />
      </svg>
    </span>
  )
}

const removeButton =
  'flex size-11 shrink-0 items-center justify-center rounded-lg text-slate-500 hover:bg-red-50 hover:text-red-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-600'

type TileProps = {
  vehicleId: string
  recordId: string
  photo: Attachment
  onRemove: (attachment: Attachment) => void
}

function PhotoTile({ vehicleId, recordId, photo, onRemove }: TileProps) {
  const visibility = useSetAttachmentVisibility(vehicleId, recordId)
  const url = attachmentContentUrl(vehicleId, photo.id)
  // Enquanto a API responde, a chave já mostra o valor pedido (senão ela volta ao antigo e pula para o novo).
  const isPublic = visibility.isPending ? visibility.variables.isPublic : photo.isPublic

  return (
    <li className="overflow-hidden rounded-lg bg-white ring-1 ring-slate-200">
      <a href={url} target="_blank" rel="noopener noreferrer" aria-label={`Abrir a foto ${photo.fileName}`} className="block">
        <img
          src={url}
          alt={`Foto: ${photo.fileName}`}
          loading="lazy"
          decoding="async"
          className="aspect-square w-full bg-slate-100 object-cover"
        />
      </a>
      <div className="flex items-center justify-between gap-1 px-2">
        <label className="flex min-h-11 flex-1 cursor-pointer items-center gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            role="switch"
            className="peer sr-only"
            checked={isPublic}
            disabled={visibility.isPending}
            onChange={(event) => visibility.mutate({ attachmentId: photo.id, isPublic: event.target.checked })}
          />
          <span
            aria-hidden="true"
            className="relative h-6 w-10 shrink-0 rounded-full bg-slate-300 transition-colors after:absolute after:top-0.5 after:left-0.5 after:size-5 after:rounded-full after:bg-white after:shadow-sm after:transition-transform peer-checked:bg-emerald-600 peer-checked:after:translate-x-4 peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-amber-500 peer-disabled:opacity-60"
          />
          No link público
        </label>
        <button type="button" aria-label={`Excluir a foto ${photo.fileName}`} onClick={() => onRemove(photo)} className={removeButton}>
          <TrashIcon />
        </button>
      </div>
      {visibility.error && (
        <p role="alert" className="px-2 pb-2 text-sm text-red-700">
          {errorMessage(visibility.error)}
        </p>
      )}
    </li>
  )
}

function DocumentRow({
  vehicleId,
  file,
  onRemove,
}: {
  vehicleId: string
  file: Attachment
  onRemove: (attachment: Attachment) => void
}) {
  const kindLabel = file.contentType === 'application/pdf' ? 'PDF' : 'Imagem'

  return (
    <li className="flex items-center gap-3 py-3">
      <FileIcon />
      <div className="min-w-0 flex-1">
        <a
          href={attachmentContentUrl(vehicleId, file.id)}
          target="_blank"
          rel="noopener noreferrer"
          className="block truncate font-medium text-slate-900 underline-offset-2 hover:underline"
        >
          {file.fileName}
        </a>
        <p className="text-sm text-slate-500">
          {kindLabel} · {formatFileSize(toNumber(file.sizeBytes) ?? 0)} · só para você
        </p>
      </div>
      <button type="button" aria-label={`Excluir o documento ${file.fileName}`} onClick={() => onRemove(file)} className={removeButton}>
        <TrashIcon />
      </button>
    </li>
  )
}

function RemoveDialog({
  vehicleId,
  recordId,
  attachment,
  onClose,
}: {
  vehicleId: string
  recordId: string
  attachment: Attachment
  onClose: () => void
}) {
  const remove = useDeleteAttachment(vehicleId, recordId)

  async function confirm() {
    try {
      await remove.mutateAsync(attachment.id)
      onClose()
    } catch {
      // O erro fica em remove.error, mostrado na janela.
    }
  }

  return (
    <Modal title="Excluir este anexo?" onClose={onClose}>
      <div className="mt-3 space-y-4">
        <p className="break-words text-slate-700">
          <strong>{attachment.fileName}</strong> será apagado de vez. Não dá para desfazer.
        </p>
        {remove.error && <Alert variant="error">{errorMessage(remove.error)}</Alert>}
        <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <Button variant="secondary" onClick={onClose}>
            Cancelar
          </Button>
          <Button variant="danger" loading={remove.isPending} onClick={() => void confirm()}>
            Excluir anexo
          </Button>
        </div>
      </div>
    </Modal>
  )
}

function UploadControls({ vehicleId, recordId, attached }: { vehicleId: string; recordId: string; attached: number }) {
  const [kind, setKind] = useState<AttachmentKind>('photo')
  const [report, setReport] = useState<UploadReport | null>(null)
  const { upload, progress } = useUploadAttachments(vehicleId, recordId)
  const camera = useRef<HTMLInputElement>(null)
  const picker = useRef<HTMLInputElement>(null)
  const busy = progress !== null
  const full = attached >= MAX_ATTACHMENTS_PER_RECORD

  async function handlePick(event: ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? [])
    // Limpa a escolha: escolher o mesmo arquivo de novo (depois de um erro, por exemplo) tem de disparar o evento.
    event.target.value = ''
    if (files.length === 0) return

    setReport(null)
    setReport(await upload(files, kind, attached))
  }

  return (
    <div className="mt-5 space-y-3 border-t border-slate-200 pt-4">
      <fieldset>
        <legend className="text-sm font-medium text-slate-700">O que você vai enviar?</legend>
        <div className="mt-1 grid grid-cols-2 gap-2">
          <OptionPill label="Foto">
            <input type="radio" name="attachment-kind" className="sr-only" checked={kind === 'photo'} onChange={() => setKind('photo')} />
          </OptionPill>
          <OptionPill label="Documento">
            <input type="radio" name="attachment-kind" className="sr-only" checked={kind === 'document'} onChange={() => setKind('document')} />
          </OptionPill>
        </div>
        <p className="mt-2 text-sm text-slate-500">
          {kind === 'photo'
            ? 'Fotos entram no link público de compartilhamento; dá para ocultar cada uma depois.'
            : 'Nota fiscal, PDF e comprovantes nunca aparecem no link público.'}
        </p>
      </fieldset>

      <div className="grid grid-cols-2 gap-3 sm:flex">
        <Button disabled={busy || full} onClick={() => camera.current?.click()}>
          Tirar foto
        </Button>
        <Button variant="secondary" disabled={busy || full} onClick={() => picker.current?.click()}>
          Escolher arquivo
        </Button>
      </div>
      <input
        ref={camera}
        type="file"
        accept={IMAGE_TYPES}
        capture="environment"
        aria-label="Foto da câmera"
        tabIndex={-1}
        className="sr-only"
        onChange={handlePick}
      />
      <input
        ref={picker}
        type="file"
        multiple
        accept={kind === 'document' ? `${IMAGE_TYPES},application/pdf` : IMAGE_TYPES}
        aria-label="Arquivos do aparelho"
        tabIndex={-1}
        className="sr-only"
        onChange={handlePick}
      />
      <p className="text-sm text-slate-500">JPG, PNG, WebP ou PDF, até 10 MB cada.</p>
      {full && <Alert variant="warning">Este registro já tem {MAX_ATTACHMENTS_PER_RECORD} anexos, o máximo. Exclua algum para enviar outro.</Alert>}

      {progress && (
        <div role="status" className="space-y-2 rounded-lg bg-slate-50 p-3 ring-1 ring-slate-200">
          <p className="break-words text-sm text-slate-700">
            Enviando {progress.current} de {progress.total}: <span className="font-medium">{progress.fileName}</span>
          </p>
          <div className="h-2 overflow-hidden rounded-full bg-slate-200" aria-hidden="true">
            <div
              className="h-full animate-pulse rounded-full bg-amber-500 transition-[width]"
              style={{ width: `${Math.max(8, ((progress.current - 1) / progress.total) * 100)}%` }}
            />
          </div>
        </div>
      )}

      {report && report.uploaded > 0 && (
        <Alert variant="success">{plural(report.uploaded, 'arquivo enviado', 'arquivos enviados')}.</Alert>
      )}
      {report && report.failures.length > 0 && (
        <Alert variant="error">
          <p className="font-medium">
            {report.failures.length === 1 ? 'Um arquivo não foi enviado:' : `${report.failures.length} arquivos não foram enviados:`}
          </p>
          <ul className="mt-1 list-disc space-y-1 pl-5">
            {report.failures.map((failure, index) => (
              <li key={`${failure.fileName}-${index}`} className="break-words">
                <strong>{failure.fileName}</strong>: {failure.message}
              </li>
            ))}
          </ul>
        </Alert>
      )}
    </div>
  )
}
