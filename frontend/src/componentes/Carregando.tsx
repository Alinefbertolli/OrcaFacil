import { Alert, Box, CircularProgress } from '@mui/material'
import { mensagemDeErro } from '../api/cliente'

/** Mostra "carregando" ou a mensagem de erro de uma consulta (evita repetir isso em toda tela). */
export function EstadoDaConsulta({ carregando, erro }: { carregando: boolean; erro: unknown }) {
  if (carregando)
    return (
      <Box sx={{ display: 'grid', placeItems: 'center', py: 6 }}>
        <CircularProgress />
      </Box>
    )
  if (erro) return <Alert severity="error">{mensagemDeErro(erro)}</Alert>
  return null
}
