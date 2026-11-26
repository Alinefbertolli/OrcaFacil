import { createContext } from 'react'
import type { UsuarioLogado } from '../api/tipos'

/**
 * O "contexto" de autenticação fica num arquivo separado dos componentes:
 * assim o Fast Refresh do Vite (atualizar a tela sem perder o estado ao salvar um arquivo) funciona direito.
 */
export interface EstadoAutenticacao {
  usuario: UsuarioLogado | null
  /** true enquanto descobre, ao abrir o sistema, se ainda existe uma sessão válida. */
  carregando: boolean
  entrar: (email: string, senha: string) => Promise<void>
  sair: () => Promise<void>
}

export const AuthContexto = createContext<EstadoAutenticacao | null>(null)
