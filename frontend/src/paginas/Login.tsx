import { Alert, Box, Button, Paper, Stack, TextField, Typography } from '@mui/material'
import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { mensagemDeErro } from '../api/cliente'
import { useAutenticacao } from '../auth/useAutenticacao'

export function Login() {
  const { usuario, entrar } = useAutenticacao()
  const navegar = useNavigate()
  const local = useLocation()
  const destino = (local.state as { de?: string } | null)?.de ?? '/'

  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  if (usuario) return <Navigate to={destino} replace />

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault() // o formulário não recarrega a página: quem cuida é o React
    setErro(null)
    setEnviando(true)
    try {
      await entrar(email, senha)
      navegar(destino, { replace: true })
    } catch (e) {
      setErro(mensagemDeErro(e))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center', bgcolor: 'background.default', p: 2 }}>
      <Paper sx={{ p: 4, width: '100%', maxWidth: 400 }}>
        <Stack component="form" spacing={2} onSubmit={aoEnviar}>
          <Box>
            <Typography variant="h5" color="primary">
              OrçaFácil
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Orçamentos rápidos para o seu cliente
            </Typography>
          </Box>
          {erro && <Alert severity="error">{erro}</Alert>}
          <TextField label="E-mail" type="email" autoComplete="email" required autoFocus
            value={email} onChange={(e) => setEmail(e.target.value)} />
          <TextField label="Senha" type="password" autoComplete="current-password" required
            value={senha} onChange={(e) => setSenha(e.target.value)} />
          <Button type="submit" variant="contained" size="large" disabled={enviando}>
            {enviando ? 'Entrando...' : 'Entrar'}
          </Button>
        </Stack>
      </Paper>
    </Box>
  )
}
