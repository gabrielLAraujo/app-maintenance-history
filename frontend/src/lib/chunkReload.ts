const KEY = 'garage:chunk-reload-at'

/** Uma segunda falha dentro deste intervalo depois de recarregar não recarrega de novo (o arquivo falta mesmo). */
const RETRY_WINDOW_MS = 10_000

type ReloadTarget = Pick<EventTarget, 'addEventListener'> & { location: Pick<Location, 'reload'> }
type SessionStore = Pick<Storage, 'getItem' | 'setItem'>

/**
 * As telas logadas vêm em arquivos à parte com hash no nome (lazyPages). Depois de um deploy, uma aba aberta antes dele
 * pede um arquivo que não existe mais e o Vite dispara `vite:preloadError`: em vez da tela de erro, recarrega a página,
 * que traz o index.html novo. Se o arquivo continuar faltando logo depois, deixa o erro seguir, sem loop.
 * `target`, `storage` e `now` existem para os testes.
 */
export function installChunkReload(
  target: ReloadTarget = window,
  storage: SessionStore = sessionStorage,
  now: () => number = Date.now,
) {
  target.addEventListener('vite:preloadError', (event) => {
    let last = 0
    try {
      last = Number(storage.getItem(KEY)) || 0
    } catch {
      // sessionStorage bloqueado: recarrega mesmo assim (no pior caso, uma vez por falha).
    }
    if (now() - last < RETRY_WINDOW_MS) return

    try {
      storage.setItem(KEY, String(now()))
    } catch {
      // idem
    }
    event.preventDefault()
    target.location.reload()
  })
}
