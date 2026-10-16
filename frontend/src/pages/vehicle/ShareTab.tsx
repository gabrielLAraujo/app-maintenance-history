import { useEffect, useState } from 'react'
import { useRevokeShareLink, useShareLinks } from '../../api/hooks/sharing'
import type { components } from '../../api/schema'
import { errorMessage } from '../../api/unwrap'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { buttonClass } from '../../components/buttonStyles'
import { Empty } from '../../components/Empty'
import { Modal } from '../../components/Modal'
import { Spinner } from '../../components/Spinner'
import { formatInstantDate } from '../../lib/format'
import { ShareLinkDialog } from './ShareLinkDialog'

type ShareLink = components['schemas']['ShareLinkDto']

/** Ativos primeiro; dentro de cada grupo, o mais novo antes. */
const ordered = (links: ShareLink[]) =>
  [...links].sort((a, b) => Number(b.isActive) - Number(a.isActive) || Date.parse(b.createdAt) - Date.parse(a.createdAt))

const nameOf = (link: ShareLink) => link.label?.trim() || 'Link sem nome'

/** O endereço que se manda a alguém: origem do app + caminho (`/p/{token}`) que a API devolve. */
const absoluteUrl = (link: ShareLink) => `${window.location.origin}${link.path}`

const chip = 'inline-flex items-center rounded-full bg-track px-2.5 py-0.5 text-xs font-medium text-ink-2'

/** Aba Compartilhar: links públicos do veículo (criar, copiar, abrir, revogar) com o status de cada um. */
export function ShareTab({ vehicleId }: { vehicleId: string }) {
  const links = useShareLinks(vehicleId)
  const [creating, setCreating] = useState(false)
  const [created, setCreated] = useState(false)
  const [revoking, setRevoking] = useState<ShareLink | null>(null)

  if (links.isPending) {
    return (
      <p className="flex items-center gap-2 py-8 text-ink-2">
        <Spinner size="sm" label={null} /> Carregando links…
      </p>
    )
  }

  if (links.error || !links.data) {
    return (
      <div className="space-y-3">
        <Alert variant="error">{errorMessage(links.error)}</Alert>
        <Button variant="secondary" onClick={() => void links.refetch()}>
          Tentar de novo
        </Button>
      </div>
    )
  }

  const items = ordered(links.data)

  function startCreating() {
    setCreated(false)
    setCreating(true)
  }

  // O aviso de "link criado" é do momento da criação: ao revogar, ele já não diz nada de útil.
  function startRevoking(link: ShareLink) {
    setCreated(false)
    setRevoking(link)
  }

  return (
    <div className="space-y-4">
      <p className="text-ink-2">
        Quem recebe o link vê, sem entrar no app, o veículo, a km e o histórico com as peças e as fotos marcadas como
        &ldquo;No link público&rdquo;. Documentos, notas fiscais, chassi, observações e lembretes nunca aparecem.
      </p>

      {created && <Alert variant="success">Link criado. Toque em &ldquo;Copiar link&rdquo; para enviá-lo.</Alert>}

      {items.length === 0 ? (
        <Empty
          title="Nenhum link ainda"
          actions={<Button onClick={startCreating}>Criar o primeiro link</Button>}
        >
          <p>Crie um link para mostrar o histórico a um comprador, mecânico ou seguradora, sem dar acesso à sua conta.</p>
        </Empty>
      ) : (
        <>
          <Button className="w-full sm:w-auto" onClick={startCreating}>
            Novo link
          </Button>
          <ul className="space-y-3">
            {items.map((link) => (
              <LinkRow key={link.id} link={link} onRevoke={() => startRevoking(link)} />
            ))}
          </ul>
        </>
      )}

      {creating && <ShareLinkDialog vehicleId={vehicleId} onClose={() => setCreating(false)} onCreated={() => setCreated(true)} />}
      {revoking && <RevokeDialog vehicleId={vehicleId} link={revoking} onClose={() => setRevoking(null)} />}
    </div>
  )
}

type CopyState = 'idle' | 'copied' | 'failed'

function LinkRow({ link, onRevoke }: { link: ShareLink; onRevoke: () => void }) {
  const name = nameOf(link)
  const url = absoluteUrl(link)
  const [copy, setCopy] = useState<CopyState>('idle')

  // "Link copiado" some sozinho: o botão volta a convidar a copiar.
  useEffect(() => {
    if (copy !== 'copied') return
    const timer = setTimeout(() => setCopy('idle'), 2500)
    return () => clearTimeout(timer)
  }, [copy])

  async function copyLink() {
    try {
      await navigator.clipboard.writeText(url)
      setCopy('copied')
    } catch {
      // Sem permissão (ou sem HTTPS): mostra o endereço pronto para selecionar.
      setCopy('failed')
    }
  }

  return (
    <li className={`rounded-card border border-line bg-surface p-4 ${link.isActive ? '' : 'opacity-80'}`}>
      <div className="flex items-start justify-between gap-3">
        <h3 className="min-w-0 font-semibold leading-snug break-words">{name}</h3>
        <StatusBadge link={link} />
      </div>
      <p className="mt-1 text-sm text-muted">Criado em {formatInstantDate(link.createdAt)}</p>

      <div className="mt-2 flex flex-wrap gap-2">
        <span className={chip}>{link.includeCosts ? 'Com custos' : 'Sem custos'}</span>
        <span className={chip}>{link.includePlate ? 'Com placa' : 'Sem placa'}</span>
        <span className={chip}>{validityText(link)}</span>
      </div>

      {link.isActive && (
        <>
          <p className="mt-3 rounded-lg bg-ground px-3 py-2 font-mono text-xs break-all text-ink-2">{url}</p>
          <div className="mt-3 grid grid-cols-2 gap-2 sm:flex">
            <Button
              className="col-span-2 sm:col-span-1"
              aria-label={copy === 'copied' ? 'Link copiado' : `Copiar link ${name}`}
              onClick={() => void copyLink()}
            >
              {copy === 'copied' ? 'Link copiado' : 'Copiar link'}
            </Button>
            <a
              href={link.path}
              target="_blank"
              rel="noopener noreferrer"
              aria-label={`Abrir ${name}`}
              className={buttonClass('secondary')}
            >
              Abrir
            </a>
            <Button variant="secondary" aria-label={`Revogar ${name}`} onClick={onRevoke}>
              Revogar
            </Button>
          </div>
          <span className="sr-only" aria-live="polite">
            {copy === 'copied' ? 'Link copiado' : ''}
          </span>
          {copy === 'failed' && (
            <div className="mt-3 space-y-2">
              <Alert variant="warning">Não foi possível copiar sozinho. Selecione o endereço abaixo e copie.</Alert>
              <input
                readOnly
                autoFocus
                aria-label={`Endereço do link ${name}`}
                value={url}
                onFocus={(event) => event.currentTarget.select()}
                className="block min-h-11 w-full rounded-lg border border-line-strong bg-white px-3 font-mono text-sm text-ink focus:outline-2 focus:outline-offset-0 focus:outline-amber-ink"
              />
            </div>
          )}
        </>
      )}
    </li>
  )
}

function StatusBadge({ link }: { link: ShareLink }) {
  const [label, look] = link.isActive
    ? (['Ativo', 'bg-ok-soft text-ok'] as const)
    : link.revokedAt
      ? (['Revogado', 'bg-danger-soft text-danger'] as const)
      : (['Expirado', 'bg-line text-ink-2'] as const)

  return <span className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-semibold ${look}`}>{label}</span>
}

/** Quando o link vale (ou valeu): validade, revogação ou expiração. */
function validityText(link: ShareLink): string {
  if (link.revokedAt) return `Revogado em ${formatInstantDate(link.revokedAt)}`
  if (link.expiresAt) return `${link.isActive ? 'Expira' : 'Expirou'} em ${formatInstantDate(link.expiresAt)}`
  return link.isActive ? 'Sem validade' : 'Inativo'
}

function RevokeDialog({ vehicleId, link, onClose }: { vehicleId: string; link: ShareLink; onClose: () => void }) {
  const revoke = useRevokeShareLink(vehicleId)

  async function confirm() {
    try {
      await revoke.mutateAsync(link.id)
      onClose()
    } catch {
      // O erro fica em revoke.error, mostrado na janela.
    }
  }

  return (
    <Modal title="Revogar este link?" onClose={onClose}>
      <div className="mt-3 space-y-4">
        <p className="break-words text-ink-2">
          <strong>{nameOf(link)}</strong> deixa de abrir na hora para quem o recebeu. Não dá para desfazer: para voltar a
          compartilhar, crie um link novo.
        </p>
        {revoke.error && <Alert variant="error">{errorMessage(revoke.error)}</Alert>}
        <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <Button variant="secondary" onClick={onClose}>
            Cancelar
          </Button>
          <Button variant="danger" loading={revoke.isPending} onClick={() => void confirm()}>
            Revogar link
          </Button>
        </div>
      </div>
    </Modal>
  )
}
