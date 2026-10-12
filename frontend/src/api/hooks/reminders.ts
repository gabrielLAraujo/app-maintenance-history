import { useQuery } from '@tanstack/react-query'
import { api } from '../client'
import { unwrap } from '../unwrap'

/** Chave do cache dos lembretes vencidos e próximos (todos os veículos). Quem mexe em km ou em lembrete a invalida. */
export const dueRemindersKey = ['reminders', 'due'] as const

/** Lembretes vencidos e próximos de todos os veículos do usuário (a lista "Atenção" do painel). */
export function useDueReminders() {
  return useQuery({
    queryKey: dueRemindersKey,
    queryFn: () => unwrap(api.GET('/api/reminders/due')),
  })
}
