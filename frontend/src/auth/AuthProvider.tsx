import { useQueryClient } from '@tanstack/react-query'
import { createContext, use, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, ensureAntiforgery, onUnauthorized } from '../api/client'
import { networkProblem, toProblem, type Problem } from '../api/problem'
import type { components } from '../api/schema'

type User = components['schemas']['MeResponse']

type AuthContextValue = {
  user: User | null
  /** Verdadeiro até saber se há sessão aberta (primeira carga): as rotas mostram um spinner, não a tela errada. */
  loading: boolean
  registrationEnabled: boolean
  /** A API oferece a demo por visitante (`POST /api/auth/demo`). */
  demoEnabled: boolean
  /** Devolve `null` quando deu certo; senão o Problem para a tela mostrar. */
  login(email: string, password: string): Promise<Problem | null>
  register(email: string, password: string): Promise<Problem | null>
  /** Abre uma conta demo, já logada, com os dados de exemplo. Mesmo retorno de `login`. */
  startDemo(): Promise<Problem | null>
  /** Encerra a sessão. `exitTo` é para onde as telas protegidas mandam depois (padrão: o login). */
  logout(exitTo?: string): Promise<void>
  /** Destino escolhido no último logout; `null` quando a sessão acabou por outro motivo (expirou). */
  exitTo: string | null
}

const AuthContext = createContext<AuthContextValue | null>(null)

const ignoreFailure = () => undefined

/** 401 aqui é a resposta normal para "ninguém logado" e vira `null`. */
async function fetchCurrentUser(): Promise<User | null> {
  const { data } = await api.GET('/api/auth/me')
  return data ?? null
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)
  const [registrationEnabled, setRegistrationEnabled] = useState(false)
  const [demoEnabled, setDemoEnabled] = useState(false)
  const [exitTo, setExitTo] = useState<string | null>(null)

  // Sessão expirada no meio do uso: limpa o usuário e os dados em cache. As rotas protegidas veem `user === null`
  // e navegam para /login (sem loop: a tela de login não faz chamadas que dêem 401 "de sessão").
  useEffect(
    () =>
      onUnauthorized(() => {
        queryClient.clear()
        setUser(null)
      }),
    [queryClient],
  )

  useEffect(() => {
    let current = true

    async function start() {
      try {
        // O token antiforgery vai junto: a primeira escrita (login) já o encontra pronto.
        const [me, config] = await Promise.all([
          fetchCurrentUser(),
          api.GET('/api/auth/config'),
          ensureAntiforgery().catch(ignoreFailure),
        ])
        if (!current) return
        setUser(me)
        setRegistrationEnabled(config.data?.registrationEnabled ?? false)
        setDemoEnabled(config.data?.demoEnabled ?? false)
      } catch {
        // API fora do ar: cai na tela de login, cuja tentativa de entrar mostra o aviso de conexão.
      } finally {
        if (current) setLoading(false)
      }
    }

    void start()
    return () => {
      current = false
    }
  }, [])

  const startSession = useCallback(
    async (request: () => Promise<{ response: Response; error?: unknown }>): Promise<Problem | null> => {
      try {
        const { response, error } = await request()
        if (!response.ok) return toProblem(response.status, error)

        // Login, cadastro e demo abrem a sessão (o cliente já renovou o token antiforgery); /me traz o usuário.
        const me = await fetchCurrentUser()
        if (!me) return { status: 401, detail: 'Não foi possível abrir a sessão. Tente de novo.' }

        setExitTo(null)
        setUser(me)
        return null
      } catch {
        return networkProblem
      }
    },
    [],
  )

  const login = useCallback(
    (email: string, password: string) =>
      startSession(() => api.POST('/api/auth/login', { body: { email, password } })),
    [startSession],
  )

  const register = useCallback(
    (email: string, password: string) =>
      startSession(() => api.POST('/api/auth/register', { body: { email, password } })),
    [startSession],
  )

  const startDemo = useCallback(() => startSession(() => api.POST('/api/auth/demo')), [startSession])

  const logout = useCallback(
    async (next = '/login') => {
      try {
        await api.POST('/api/auth/logout')
      } catch {
        // Sem rede não dá para avisar a API; a tela sai da sessão mesmo assim.
      }
      queryClient.clear()
      setExitTo(next)
      setUser(null)
    },
    [queryClient],
  )

  const value = useMemo(
    () => ({ user, loading, registrationEnabled, demoEnabled, login, register, startDemo, logout, exitTo }),
    [user, loading, registrationEnabled, demoEnabled, login, register, startDemo, logout, exitTo],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}

// oxlint-disable-next-line react/only-export-components -- o hook mora junto do provider cujo contexto ele lê
export function useAuth(): AuthContextValue {
  const context = use(AuthContext)
  if (!context) throw new Error('useAuth precisa ser usado dentro de <AuthProvider>.')
  return context
}
