import { Box, CircularProgress } from '@mui/material'
import { Navigate, Outlet, useLocation } from 'react-router'
import { useAutenticacao } from './useAutenticacao'

/** Só deixa entrar nas telas internas quem está logado; os outros vão para o login. */
export function RotaProtegida() {
  const { usuario, carregando } = useAutenticacao()
  const local = useLocation()

  if (carregando)
    return (
      <Box sx={{ display: 'grid', placeItems: 'center', height: '100vh' }}>
        <CircularProgress />
      </Box>
    )

  // "state.de" lembra para onde a pessoa queria ir, para voltar lá depois do login.
  if (!usuario) return <Navigate to="/login" replace state={{ de: local.pathname }} />
  return <Outlet />
}
