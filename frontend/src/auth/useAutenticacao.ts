import { useContext } from 'react'
import { AuthContexto, type EstadoAutenticacao } from './contexto'

/** Atalho para as telas: const { usuario, sair } = useAutenticacao() */
export function useAutenticacao(): EstadoAutenticacao {
  const contexto = useContext(AuthContexto)
  if (!contexto) throw new Error('useAutenticacao precisa estar dentro de <AuthProvider>.')
  return contexto
}
