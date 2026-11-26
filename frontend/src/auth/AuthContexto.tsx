import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { renovarSessao, sessao } from '../api/cliente'
import { autenticacao } from '../api/endpoints'
import type { UsuarioLogado } from '../api/tipos'
import { AuthContexto } from './contexto'

/** Guarda quem está logado e oferece isso a qualquer tela (via useAutenticacao). */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<UsuarioLogado | null>(null)
  const [carregando, setCarregando] = useState(true)

  useEffect(() => {
    // Se a sessão acabar de vez (refresh recusado), volta para o login.
    sessao.quandoExpirar(() => setUsuario(null))

    // Ao abrir (ou dar F5), tenta continuar a sessão anterior com o refresh token salvo.
    renovarSessao()
      .then((resposta) => setUsuario(resposta?.usuario ?? null))
      .finally(() => setCarregando(false))
  }, [])

  const entrar = useCallback(async (email: string, senha: string) => {
    setUsuario(await autenticacao.entrar(email, senha))
  }, [])

  const sair = useCallback(async () => {
    await autenticacao.sair()
    setUsuario(null)
  }, [])

  const valor = useMemo(() => ({ usuario, carregando, entrar, sair }), [usuario, carregando, entrar, sair])
  return <AuthContexto value={valor}>{children}</AuthContexto>
}
