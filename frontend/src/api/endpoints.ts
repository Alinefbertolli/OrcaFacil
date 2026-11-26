import { http, sessao } from './cliente'
import type {
  Categoria,
  CategoriaRequisicao,
  Cliente,
  ClienteRequisicao,
  ClienteResumo,
  ConfiguracoesEmpresa,
  ConfiguracoesEmpresaRequisicao,
  EstoqueResposta,
  EstoqueResumo,
  Fornecedor,
  FornecedorRequisicao,
  Marca,
  MarcaRequisicao,
  Movimentacao,
  ProdutoDetalhe,
  ProdutoRequisicao,
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
  ResultadoImportacaoCatalogo,
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

// ---------------------------------------------------------------- Fase 11

/** Monta a "query string" (?a=1&b=2) ignorando valores vazios. */
function consulta(parametros: Record<string, string | number | boolean | null | undefined>) {
  const p = new URLSearchParams()
  for (const [chave, valor] of Object.entries(parametros))
    if (valor !== null && valor !== undefined && valor !== '' && valor !== false) p.set(chave, String(valor))
  const texto = p.toString()
  return texto ? `?${texto}` : ''
}

export interface FiltroLista {
  pagina?: number
  busca?: string
  incluirInativos?: boolean
  tamanhoPagina?: number
}

export const categorias = {
  listar: (f: FiltroLista = {}) =>
    http.get<Pagina<Categoria>>(`/categorias${consulta({ busca: f.busca, pagina: f.pagina, tamanhoPagina: f.tamanhoPagina ?? 20 })}`),
  todas: () => http.get<Pagina<Categoria>>('/categorias?tamanhoPagina=100'),
  criar: (r: CategoriaRequisicao) => http.post<Categoria>('/categorias', r),
  atualizar: (id: string, r: CategoriaRequisicao) => http.put<Categoria>(`/categorias/${id}`, r),
  excluir: (id: string) => http.delete(`/categorias/${id}`),
}

export const marcas = {
  listar: (f: FiltroLista = {}) =>
    http.get<Pagina<Marca>>(`/marcas${consulta({ busca: f.busca, pagina: f.pagina, tamanhoPagina: f.tamanhoPagina ?? 20 })}`),
  todas: () => http.get<Pagina<Marca>>('/marcas?tamanhoPagina=100'),
  criar: (r: MarcaRequisicao) => http.post<Marca>('/marcas', r),
  atualizar: (id: string, r: MarcaRequisicao) => http.put<Marca>(`/marcas/${id}`, r),
  excluir: (id: string) => http.delete(`/marcas/${id}`),
}

export const produtos = {
  listar: (f: FiltroLista = {}) =>
    http.get<Pagina<ProdutoResumo>>(
      `/produtos${consulta({ busca: f.busca, pagina: f.pagina, incluirInativos: f.incluirInativos, tamanhoPagina: 20 })}`,
    ),
  obter: (id: string) => http.get<ProdutoDetalhe>(`/produtos/${id}`),
  criar: (r: ProdutoRequisicao) => http.post<ProdutoDetalhe>('/produtos', r),
  atualizar: (id: string, r: ProdutoRequisicao) => http.put<ProdutoDetalhe>(`/produtos/${id}`, r),
  desativar: (id: string) => http.delete(`/produtos/${id}`),
  reativar: (id: string) => http.post<ProdutoDetalhe>(`/produtos/${id}/reativar`),
  /** simular = true: só a prévia, nada é gravado. */
  importar: (arquivo: File, simular: boolean) => {
    const formulario = new FormData()
    formulario.append('arquivo', arquivo)
    return http.post<ResultadoImportacaoCatalogo>(`/produtos/importacao?simular=${simular}`, formulario)
  },
  baixarModelo: () => http.arquivo('/produtos/importacao/modelo'),
}

export const clientes = {
  listar: (f: FiltroLista = {}) =>
    http.get<Pagina<Cliente>>(`/clientes${consulta({ busca: f.busca, pagina: f.pagina, incluirInativos: f.incluirInativos, tamanhoPagina: 20 })}`),
  criar: (r: ClienteRequisicao) => http.post<Cliente>('/clientes', r),
  atualizar: (id: string, r: ClienteRequisicao) => http.put<Cliente>(`/clientes/${id}`, r),
  desativar: (id: string) => http.delete(`/clientes/${id}`),
  reativar: (id: string) => http.post<Cliente>(`/clientes/${id}/reativar`),
}

export const fornecedores = {
  listar: (f: FiltroLista = {}) =>
    http.get<Pagina<Fornecedor>>(
      `/fornecedores${consulta({ busca: f.busca, pagina: f.pagina, incluirInativos: f.incluirInativos, tamanhoPagina: 20 })}`,
    ),
  criar: (r: FornecedorRequisicao) => http.post<Fornecedor>('/fornecedores', r),
  atualizar: (id: string, r: FornecedorRequisicao) => http.put<Fornecedor>(`/fornecedores/${id}`, r),
  desativar: (id: string) => http.delete(`/fornecedores/${id}`),
  reativar: (id: string) => http.post<Fornecedor>(`/fornecedores/${id}/reativar`),
}

export const estoque = {
  listar: (f: FiltroLista & { abaixoDoMinimo?: boolean } = {}) =>
    http.get<Pagina<EstoqueResumo>>(
      `/estoque${consulta({ busca: f.busca, pagina: f.pagina, abaixoDoMinimo: f.abaixoDoMinimo, tamanhoPagina: 20 })}`,
    ),
  obter: (produtoId: string) => http.get<EstoqueResposta>(`/estoque/${produtoId}`),
  entrada: (produtoId: string, quantidade: number, motivo: string | null) =>
    http.post<EstoqueResposta>(`/estoque/${produtoId}/entradas`, { quantidade, motivo }),
  saida: (produtoId: string, quantidade: number, motivo: string | null) =>
    http.post<EstoqueResposta>(`/estoque/${produtoId}/saidas`, { quantidade, motivo }),
  ajuste: (produtoId: string, saldoContado: number, motivo: string) =>
    http.post<EstoqueResposta>(`/estoque/${produtoId}/ajustes`, { saldoContado, motivo }),
  configurar: (produtoId: string, estoqueMinimo: number, localizacao: string | null) =>
    http.put<EstoqueResposta>(`/estoque/${produtoId}/configuracao`, { estoqueMinimo, localizacao }),
  movimentacoes: (produtoId: string, pagina = 1) =>
    http.get<Pagina<Movimentacao>>(`/estoque/${produtoId}/movimentacoes?pagina=${pagina}&tamanhoPagina=20`),
}

export const empresa = {
  configuracoes: () => http.get<ConfiguracoesEmpresa>('/empresa/configuracoes'),
  salvar: (r: ConfiguracoesEmpresaRequisicao) => http.put<ConfiguracoesEmpresa>('/empresa/configuracoes', r),
}
