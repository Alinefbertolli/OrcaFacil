import { createContext } from 'react'

/** Avisos rápidos no rodapé ("Produto salvo.", "Não foi possível..."), disponíveis para qualquer tela. */
export interface Avisos {
  sucesso: (mensagem: string) => void
  erro: (erroOuMensagem: unknown) => void
}

export const AvisosContexto = createContext<Avisos | null>(null)
