import createClient, { type Middleware } from 'openapi-fetch'
import type { paths } from './schema'

const TOKEN_COOKIE = 'XSRF-TOKEN'
const TOKEN_HEADER = 'X-XSRF-TOKEN'
const WRITE_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE'])

// 401 esperado, não é sessão expirada: senha errada no login e "ninguém logado" no /me (a primeira visita).
const EXPECTED_401 = new Set(['/api/auth/login', '/api/auth/me'])

// As rotas públicas (/api/public/{token}...) são anônimas: um 401 ali nunca significa "sessão expirada" e não pode
// mandar ninguém para o login (quem abre um link compartilhado nem tem conta).
const PUBLIC_PREFIX = '/api/public/'

// O token antiforgery é vinculado ao usuário: depois de abrir ou encerrar a sessão precisa ser buscado de novo,
// senão a próxima escrita volta 400 auth.antiforgery_invalid.
const IDENTITY_CHANGES = new Set(['/api/auth/login', '/api/auth/register', '/api/auth/logout'])

let unauthorizedHandler: (() => void) | null = null
let tokenRequest: Promise<void> | null = null

/**
 * O único cliente da API: mesma origem (a API serve o front; em desenvolvimento o Vite faz proxy), cookie de sessão
 * em toda chamada, `X-XSRF-TOKEN` em toda escrita e 401 repassado a `onUnauthorized`.
 */
export const api = createClient<paths>({
  // Mesma origem. Vai explícita (e não '') porque o Request do Node, usado nos testes, não aceita URL relativa.
  baseUrl: window.location.origin,
  credentials: 'include',
  // Lê o fetch global a cada chamada (e não ao criar o cliente): os testes trocam o fetch por um falso.
  fetch: (request) => globalThis.fetch(request),
})

/**
 * Registra quem cuida da sessão expirada (o AuthProvider limpa o usuário e o router leva ao /login). Só há um:
 * registrar de novo substitui o anterior. Devolve a função que cancela o registro.
 */
export function onUnauthorized(handler: () => void): () => void {
  unauthorizedHandler = handler
  return () => {
    if (unauthorizedHandler === handler) unauthorizedHandler = null
  }
}

/**
 * Garante o cookie `XSRF-TOKEN` (busca `GET /api/auth/antiforgery`). Chamadas seguidas reaproveitam a mesma busca;
 * `force` busca um token novo. Se a busca falhar, a promessa rejeita e a próxima chamada tenta de novo.
 */
export function ensureAntiforgery(force = false): Promise<void> {
  if (force || !tokenRequest) {
    const request = requestToken()
    tokenRequest = request
    request.catch(() => {
      if (tokenRequest === request) tokenRequest = null
    })
  }
  return tokenRequest
}

async function requestToken(): Promise<void> {
  const { response } = await api.GET('/api/auth/antiforgery')
  if (!response.ok) throw new Error(`Não foi possível obter o token antiforgery (HTTP ${response.status}).`)
}

function readCookie(name: string): string | undefined {
  const prefix = `${name}=`
  const entry = document.cookie.split('; ').find((cookie) => cookie.startsWith(prefix))
  // O ASP.NET grava o valor codificado (Uri.EscapeDataString); o cabeçalho leva o token puro.
  return entry ? decodeURIComponent(entry.slice(prefix.length)) : undefined
}

const ignoreFailure = () => undefined

async function readToken(): Promise<string | undefined> {
  await ensureAntiforgery().catch(ignoreFailure)
  let token = readCookie(TOKEN_COOKIE)

  if (!token) {
    // O cookie sumiu depois do carregamento inicial (limpo, expirado): busca de novo.
    await ensureAntiforgery(true).catch(ignoreFailure)
    token = readCookie(TOKEN_COOKIE)
  }

  return token
}

const middleware: Middleware = {
  async onRequest({ request }) {
    if (!WRITE_METHODS.has(request.method)) return

    // Sem token a API responde 400 auth.antiforgery_invalid, com mensagem pronta para o usuário.
    const token = await readToken()
    if (token) request.headers.set(TOKEN_HEADER, token)
  },

  async onResponse({ response, schemaPath }) {
    if (response.status === 401 && !EXPECTED_401.has(schemaPath) && !schemaPath.startsWith(PUBLIC_PREFIX)) unauthorizedHandler?.()

    // Termina antes de a chamada de login/cadastro/logout voltar para quem a fez: a próxima escrita já usa o token novo.
    if (response.ok && IDENTITY_CHANGES.has(schemaPath)) await ensureAntiforgery(true).catch(ignoreFailure)
  },
}

api.use(middleware)
