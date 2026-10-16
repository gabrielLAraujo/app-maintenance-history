import { describe, expect, it, vi } from 'vitest'
import { installChunkReload } from './chunkReload'

function fakeWindow() {
  const target = new EventTarget()
  return Object.assign(target, { location: { reload: vi.fn() } })
}

function memoryStorage(initial: Record<string, string> = {}) {
  const data = new Map(Object.entries(initial))
  return {
    getItem: (key: string) => data.get(key) ?? null,
    setItem: (key: string, value: string) => void data.set(key, value),
  }
}

// O evento que o Vite dispara quando um import dinâmico falha (arquivo de tela com hash antigo, depois de um deploy).
const preloadError = () => new Event('vite:preloadError', { cancelable: true })

describe('arquivo de tela que sumiu depois de um deploy', () => {
  it('recarrega a página uma vez, no lugar da tela de erro', () => {
    const win = fakeWindow()
    installChunkReload(win, memoryStorage(), () => 1_000_000)

    const event = preloadError()
    win.dispatchEvent(event)

    expect(win.location.reload).toHaveBeenCalledTimes(1)
    expect(event.defaultPrevented).toBe(true)
  })

  it('não entra em loop: se falhar de novo logo depois de recarregar, deixa o erro seguir', () => {
    const win = fakeWindow()
    const storage = memoryStorage()
    let now = 1_000_000
    installChunkReload(win, storage, () => now)
    win.dispatchEvent(preloadError())

    // A página recarregou (o mesmo sessionStorage) e o arquivo continua faltando 2 s depois.
    const reloaded = fakeWindow()
    now += 2_000
    installChunkReload(reloaded, storage, () => now)
    const again = preloadError()
    reloaded.dispatchEvent(again)

    expect(reloaded.location.reload).not.toHaveBeenCalled()
    expect(again.defaultPrevented).toBe(false)
  })

  it('um deploy mais tarde volta a recarregar', () => {
    const win = fakeWindow()
    let now = 1_000_000
    installChunkReload(win, memoryStorage(), () => now)
    win.dispatchEvent(preloadError())

    now += 60_000
    win.dispatchEvent(preloadError())

    expect(win.location.reload).toHaveBeenCalledTimes(2)
  })

  it('sem sessionStorage (navegação privada bloqueada), ainda recarrega', () => {
    const win = fakeWindow()
    const broken = {
      getItem: () => {
        throw new Error('SecurityError')
      },
      setItem: () => {
        throw new Error('SecurityError')
      },
    }
    installChunkReload(win, broken, () => 1_000_000)

    win.dispatchEvent(preloadError())

    expect(win.location.reload).toHaveBeenCalledTimes(1)
  })
})
