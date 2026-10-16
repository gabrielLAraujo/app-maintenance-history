import { json, noContent, type Handler } from './fetch-mock'

export const account = {
  id: '0198a1c2-0000-7000-8000-000000000001',
  email: 'gabriel@example.com',
  isDemo: false,
  demoExpiresAt: null,
}

/** As rotas de sessão de quem já está logado (token antiforgery, config e /me): os testes de tela somam as suas. */
export function signedInRoutes(): Record<string, Handler> {
  return {
    'GET /api/auth/antiforgery': () => {
      document.cookie = 'XSRF-TOKEN=test-token; path=/'
      return noContent()
    },
    'GET /api/auth/config': () => json({ registrationEnabled: false, demoEnabled: false }),
    'GET /api/auth/me': () => json(account),
  }
}
