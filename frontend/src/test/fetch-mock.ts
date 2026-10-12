import { vi } from 'vitest'

/** Uma requisição que chegou ao `fetch` falso, na ordem em que foi feita. */
export type Call = {
  method: string
  /** Caminho + query, sem a origem (ex.: `/api/vehicles?x=1`). */
  path: string
  headers: Headers
  credentials: RequestCredentials
  body: string
}

type Handler = (call: Call) => Response | Promise<Response>

export const noContent = () => new Response(null, { status: 204 })

export const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

export const problem = (status: number, body: Record<string, unknown>) =>
  new Response(JSON.stringify({ status, ...body }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })

export function resetCookies() {
  for (const cookie of document.cookie.split('; ')) {
    const name = cookie.split('=')[0]
    if (name) document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/`
  }
}

/**
 * Troca o `fetch` global por um servidor falso. As rotas são "MÉTODO /caminho" (sem query); o handler devolve uma
 * Response nova a cada chamada. Requisição sem rota é erro do teste: falha alto em vez de devolver 404 silencioso.
 */
export function mockFetch(routes: Record<string, Handler>) {
  const calls: Call[] = []

  vi.stubGlobal('fetch', async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init)
    const url = new URL(request.url)
    const call: Call = {
      method: request.method,
      path: url.pathname + url.search,
      headers: request.headers,
      credentials: request.credentials,
      body: await request.clone().text(),
    }
    calls.push(call)

    const handler = routes[`${request.method} ${url.pathname}`]
    if (!handler) throw new Error(`Requisição sem rota no fetch falso: ${request.method} ${url.pathname}`)
    return handler(call)
  })

  return calls
}
