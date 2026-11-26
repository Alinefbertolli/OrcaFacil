import { http, sessao } from './cliente'
import type {
  ClienteResumo,
  Comparacao,
  FormatoImportacao,
  GerarOrcamentoRequisicao,
  ItemListaRequisicao,
  ListaImportada,
  ListaImportadaResumo,
  MensagemWhatsApp,
  Orcamento,
  OrcamentoResumo,
  Pagina,
  ProdutoResumo,
  RespostaAutenticacao,
  StatusOrcamento,
  UsuarioLogado,
} from './tipos'

// Uma função por endpoint usado no frontend: as telas nunca escrevem URLs "na mão".

export const autenticacao = {
  async entrar(email: string, senha: string) {
    const resposta = await http.post<RespostaAutenticacao>('/auth/login', { email, senha })
    sessao.iniciar(resposta)
    return resposta.usuario
  },
  async sair() {
    const refreshToken = sessao.refreshToken()
    try {
      if (refreshToken) await http.post<void>('/auth/logout', { refreshToken })
    } finally {
      sessao.encerrar()
    }
  },
  eu: () => http.get<UsuarioLogado>('/usuarios/me'),
}

export const importacoes = {
  listar: (pagina = 1) => http.get<Pagina<ListaImportadaResumo>>(`/importacoes?pagina=${pagina}&tamanhoPagina=20`),
  obter: (id: string) => http.get<ListaImportada>(`/importacoes/${id}`),
  importarTexto: (texto: string, formato: FormatoImportacao, clienteId: string | null) =>
    http.post<ListaImportada>('/importacoes/texto', { texto, formato, clienteId }),
  importarArquivo: (arquivo: File, clienteId: string | null) => {
    const formulario = new FormData()
    formulario.append('arquivo', arquivo)
    if (clienteId) formulario.append('clienteId', clienteId)
    return http.post<ListaImportada>('/importacoes/arquivo', formulario)
  },
  atualizarItem: (id: string, itemId: string, item: ItemListaRequisicao) =>
    http.put<ListaImportada>(`/importacoes/${id}/itens/${itemId}`, item),
  adicionarItem: (id: string, item: ItemListaRequisicao) => http.post<ListaImportada>(`/importacoes/${id}/itens`, item),
  excluirItem: (id: string, itemId: string) => http.delete(`/importacoes/${id}/itens/${itemId}`),
  confirmarRevisao: (id: string) => http.post<ListaImportada>(`/importacoes/${id}/confirmar-revisao`),
}

export const catalogo = {
  buscarProdutos: (busca: string) =>
    http.get<Pagina<ProdutoResumo>>(`/produtos?busca=${encodeURIComponent(busca)}&tamanhoPagina=20`),
  buscarClientes: (busca: string) =>
    http.get<Pagina<ClienteResumo>>(`/clientes?busca=${encodeURIComponent(busca)}&tamanhoPagina=20`),
}

export const orcamentos = {
  listar: (status: StatusOrcamento | '' = '', pagina = 1) =>
    http.get<Pagina<OrcamentoResumo>>(`/orcamentos?pagina=${pagina}&tamanhoPagina=20${status ? `&status=${status}` : ''}`),
  obter: (id: string) => http.get<Orcamento>(`/orcamentos/${id}`),
  gerar: (requisicao: GerarOrcamentoRequisicao) => http.post<Orcamento>('/orcamentos/gerar', requisicao),
  comparar: (id: string) => http.get<Comparacao>(`/orcamentos/${id}/comparacao`),
  informarPreco: (id: string, opcaoId: string, itemId: string, precoUnitario: number) =>
    http.put<Orcamento>(`/orcamentos/${id}/opcoes/${opcaoId}/itens/${itemId}/preco`, { precoUnitario }),
  selecionar: (id: string, opcaoId: string) => http.post<Orcamento>(`/orcamentos/${id}/opcoes/${opcaoId}/selecionar`),
  alterarStatus: (id: string, status: StatusOrcamento) => http.post<Orcamento>(`/orcamentos/${id}/status`, { status }),
  pdf: (id: string) => http.arquivo(`/orcamentos/${id}/pdf`),
  whatsapp: (id: string) => http.get<MensagemWhatsApp>(`/orcamentos/${id}/whatsapp`),
}
