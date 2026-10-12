import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// API local (perfil "http" do launchSettings.json). O navegador só fala com o Vite: cookie e antiforgery
// funcionam como em produção, onde a própria API serve o front no mesmo domínio.
const api = 'http://localhost:5081'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: { '/api': api, '/openapi': api, '/scalar': api },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
