import { useAutenticacao } from './useAutenticacao'

/**
 * O que o usuário logado pode fazer. A API é quem garante as regras (403 se não puder);
 * aqui só escondemos ou desabilitamos os botões para a tela não oferecer o que vai ser recusado.
 */
export function usePermissoes() {
  const { usuario } = useAutenticacao()
  const perfil = usuario?.perfil
  return {
    ehGerente: perfil === 'Gerente' || perfil === 'Administrador',
    ehAdministrador: perfil === 'Administrador',
  }
}
