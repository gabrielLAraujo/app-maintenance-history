import { json, noContent, type Handler } from './fetch-mock'

export const account = {
  id: '0198a1c2-0000-7000-8000-000000000001',
  email: 'gabriel@example.com',
  isDemo: false,
  demoExpiresAt: null,
}

export const emptyGarageSummary = {
  year: 2026,
  yearCost: 0,
  yearRecordCount: 0,
  yearVehicleCount: 0,
  byMonth: Array.from({ length: 12 }, (_, index) => ({ month: `2026-${String(index + 1).padStart(2, '0')}`, cost: 0 })),
  recentRecords: [],
  recordCount: 0,
  firstRecordDate: null,
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
    // O contador de vencidos do menu (AppShell) busca os lembretes em toda tela logada.
    'GET /api/reminders/due': () => json([]),
    // O painel (/) busca o resumo do ano; aqui, uma garagem sem gastos.
    'GET /api/garage/summary': () => json(emptyGarageSummary),
  }
}
