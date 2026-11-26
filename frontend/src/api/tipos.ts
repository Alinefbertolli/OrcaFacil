// Tipos que espelham os DTOs da API (.NET). Os nomes chegam em camelCase e os enums como texto.
// "type" com uniões de texto no lugar de "enum": o projeto usa erasableSyntaxOnly (TypeScript só com tipos).

export type Perfil = 'Operador' | 'Gerente' | 'Administrador'

export interface UsuarioLogado {
  id: string
  nome: string
  email: string
  perfil: Perfil
  empresaId: string
  empresaNome: string
}

export interface RespostaAutenticacao {
  tokenAcesso: string
  tokenAcessoExpiraEm: string
  refreshToken: string
  refreshTokenExpiraEm: string
  usuario: UsuarioLogado
}

export interface Pagina<T> {
  itens: T[]
  pagina: number
  tamanhoPagina: number
  totalItens: number
  totalPaginas: number
  temAnterior: boolean
  temProxima: boolean
}

// ---------------------------------------------------------------- Importação de listas

export type FormatoImportacao = 'Pdf' | 'Imagem' | 'Excel' | 'Word' | 'Texto' | 'WhatsApp'
export type StatusImportacao = 'Processando' | 'AguardandoRevisao' | 'Revisada' | 'Erro'

export interface ItemImportado {
  id: string
  ordem: number
  textoOriginal: string | null
  descricao: string
  quantidade: number
  unidadeMedida: string
  marca: string | null
  produtoId: string | null
  produtoCodigo: string | null
  produtoNome: string | null
  confianca: number
  precisaAtencao: boolean
}

export interface ListaImportada {
  id: string
  formato: FormatoImportacao
  nomeArquivoOriginal: string | null
  status: StatusImportacao
  mensagemErro: string | null
  clienteId: string | null
  criadoEm: string
  totalItens: number
  itensQuePrecisamAtencao: number
  itens: ItemImportado[]
}

export interface ListaImportadaResumo {
  id: string
  formato: FormatoImportacao
  nomeArquivoOriginal: string | null
  status: StatusImportacao
  clienteId: string | null
  criadoEm: string
}

export interface ItemListaRequisicao {
  descricao: string
  quantidade: number
  unidadeMedida: string
  marca: string | null
  produtoId: string | null
}

// ---------------------------------------------------------------- Cadastros (só o necessário nesta fase)

export interface ProdutoResumo {
  id: string
  codigo: string
  nome: string
  unidadeMedida: string
  categoriaId: string
  categoriaNome: string | null
  marcaId: string | null
  marcaNome: string | null
  precoVenda: number | null
  grupoEquivalencia: string | null
  ativo: boolean
}

export interface ClienteResumo {
  id: string
  nome: string
  telefone: string | null
}

// ---------------------------------------------------------------- Orçamentos

export type ModoOrcamento = 'Unico' | 'Comparativo'
export type TipoOpcao = 'CustoBeneficio' | 'MarcasReconhecidas' | 'MenorPreco' | 'Manual' | 'Unica'
export type StatusOrcamento = 'EmElaboracao' | 'AguardandoAprovacao' | 'Aprovado' | 'Reprovado' | 'Finalizado'
export type Disponibilidade = 'NaoControlado' | 'Disponivel' | 'Parcial' | 'Indisponivel'

export interface ItemOrcamento {
  id: string
  ordem: number
  itemListaImportadaId: string | null
  produtoId: string | null
  descricao: string
  marca: string | null
  quantidade: number
  unidadeMedida: string
  precoUnitario: number | null
  precoTotal: number | null
  origemPreco: string | null
  semPreco: boolean
  observacao: string | null
  disponibilidade: Disponibilidade | null
  saldoEmEstoque: number | null
}

export interface OpcaoOrcamento {
  id: string
  tipo: TipoOpcao
  nome: string
  valorTotal: number
  itensSemPreco: number
  selecionada: boolean
  itens: ItemOrcamento[]
}

export interface Orcamento {
  id: string
  numero: string
  modo: ModoOrcamento
  status: StatusOrcamento
  clienteId: string | null
  clienteNome: string | null
  listaImportadaId: string | null
  validadeAte: string
  criadoEm: string
  economia: number
  opcoes: OpcaoOrcamento[]
  avisos: string[]
}

export interface OrcamentoResumo {
  id: string
  numero: string
  modo: ModoOrcamento
  status: StatusOrcamento
  clienteId: string | null
  validadeAte: string
  criadoEm: string
}

export interface ColunaComparacao {
  opcaoId: string
  tipo: TipoOpcao
  nome: string
  valorTotal: number
  itensSemPreco: number
  diferencaParaMaisBarata: number
  diferencaPercentual: number
  maisBarata: boolean
  selecionada: boolean
}

export interface Comparacao {
  orcamentoId: string
  numero: string
  opcoes: ColunaComparacao[]
}

export interface GerarOrcamentoRequisicao {
  listaImportadaId: string
  modo?: ModoOrcamento
  opcoes?: TipoOpcao[]
  clienteId?: string | null
  validadeDias?: number
}

export interface MensagemWhatsApp {
  texto: string
  link: string
  telefoneCliente: string | null
}
