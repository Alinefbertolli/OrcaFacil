import { useContext } from 'react'
import { AvisosContexto, type Avisos } from './contexto'

export function useAvisos(): Avisos {
  const contexto = useContext(AvisosContexto)
  if (!contexto) throw new Error('useAvisos precisa estar dentro de <AvisosProvider>.')
  return contexto
}
