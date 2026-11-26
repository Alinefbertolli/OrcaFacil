import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // "Proxy": o navegador chama /api no próprio Vite (localhost:5173), e o Vite repassa para a API .NET.
    // Assim o navegador acha que é tudo o mesmo site: sem problemas de CORS nem de certificado.
    proxy: {
      '/api': {
        target: 'https://localhost:7080',
        changeOrigin: true,
        secure: false, // aceita o certificado de desenvolvimento do .NET
      },
    },
  },
})
