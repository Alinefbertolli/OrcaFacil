import { CssBaseline, ThemeProvider } from '@mui/material'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import { ErroApi } from './api/cliente'
import App from './App'
import { AuthProvider } from './auth/AuthContexto'
import { AvisosProvider } from './componentes/avisos/AvisosProvider'
import { tema } from './tema'

// TanStack Query: guarda as respostas da API em "cache" e controla carregando/erro em cada tela.
const clienteQuery = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      // Erros 4xx (ex.: 404, 422) não adiantam tentar de novo; falha de rede tenta mais uma vez.
      retry: (tentativas, erro) => !(erro instanceof ErroApi && erro.status < 500) && tentativas < 1,
    },
  },
})

// "Provedores": cada um envolve o App e oferece um recurso para todas as telas.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider theme={tema}>
      <CssBaseline />
      <QueryClientProvider client={clienteQuery}>
        <BrowserRouter>
          <AuthProvider>
            <AvisosProvider>
              <App />
            </AvisosProvider>
          </AuthProvider>
        </BrowserRouter>
      </QueryClientProvider>
    </ThemeProvider>
  </StrictMode>,
)
