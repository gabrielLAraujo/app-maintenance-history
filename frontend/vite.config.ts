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
  build: {
    rolldownOptions: {
      output: {
        // React fica num arquivo próprio: raramente muda, então o navegador o mantém em cache entre os deploys
        // (um push por tela nova) e o arquivo do app não passa do limite de 500 kB do aviso do Vite.
        codeSplitting: { groups: [{ name: 'react', test: /node_modules[\\/](react|react-dom|scheduler)[\\/]/ }] },
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
