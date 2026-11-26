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

// ---------------------------------------------------------------- Fase 11: cadastros, estoque e configurações

export type TipoPessoa = 'Fisica' | 'Juridica'
export type SegmentoEmpresa = string

export interface Endereco {
  logradouro: string | null
  numero: string | null
  complemento: string | null
  bairro: string | null
  cidade: string | null
  uf: string | null
  cep: string | null
}

export interface Categoria {
  id: string
  nome: string
  descricao: string | null
  categoriaPaiId: string | null
  categoriaPaiNome: string | null
}
export interface CategoriaRequisicao {
  nome: string
  descricao: string | null
  categoriaPaiId: string | null
}

export interface Marca {
  id: string
  nome: string
  nivelReconhecimento: number
}
export interface MarcaRequisicao {
  nome: string
  nivelReconhecimento: number
}

export interface ProdutoRequisicao {
  codigo: string
  nome: string
  descricao: string | null
  unidadeMedida: string
  categoriaId: string
  marcaId: string | null
  precoVenda: number | null
  grupoEquivalencia: string | null
}
export interface ProdutoDetalhe extends ProdutoResumo {
  descricao: string | null
}

export interface Cliente {
  id: string
  nome: string
  tipoPessoa: TipoPessoa
  documento: string | null
  telefone: string | null
  email: string | null
  endereco: Endereco | null
  observacoes: string | null
  ativo: boolean
}
export type ClienteRequisicao = Omit<Cliente, 'id' | 'ativo'>

export interface Fornecedor {
  id: string
  razaoSocial: string
  nomeFantasia: string | null
  cnpj: string
  contato: string | null
  telefone: string | null
  email: string | null
  endereco: Endereco | null
  avaliacao: number | null
  ativo: boolean
}
export type FornecedorRequisicao = Omit<Fornecedor, 'id' | 'ativo'>

export interface EstoqueResumo {
  produtoId: string
  produtoCodigo: string
  produtoNome: string
  unidadeMedida: string
  quantidadeDisponivel: number
  estoqueMinimo: number
  abaixoDoMinimo: boolean
  localizacao: string | null
}
export interface EstoqueResposta extends EstoqueResumo {
  controlado: boolean
}
export type TipoMovimentacao = 'Entrada' | 'Saida' | 'Ajuste'
export interface Movimentacao {
  id: string
  tipo: TipoMovimentacao
  quantidade: number
  saldoAnterior: number
  saldoPosterior: number
  motivo: string | null
  usuarioId: string
  orcamentoId: string | null
  criadoEm: string
}

export interface ConfiguracoesEmpresa {
  empresaId: string
  nome: string
  razaoSocial: string
  cnpj: string
  segmento: SegmentoEmpresa
  modoOrcamentoPadrao: ModoOrcamento
  telefone: string | null
  email: string | null
  endereco: Endereco | null
}
export interface ConfiguracoesEmpresaRequisicao {
  modoOrcamentoPadrao: ModoOrcamento
  telefone: string | null
  email: string | null
  endereco: Endereco | null
}

// ---------------------------------------------------------------- Importação de produtos por planilha (Fase 11.5)

export type SituacaoLinhaImportacao = 'Criado' | 'Atualizado' | 'SemAlteracao' | 'Erro'

export interface LinhaImportacaoCatalogo {
  linha: number
  codigo: string | null
  nome: string | null
  situacao: SituacaoLinhaImportacao
  detalhes: string[]
}

export interface ResultadoImportacaoCatalogo {
  /** true = só a prévia (nada foi gravado). */
  simulacao: boolean
  nomeArquivo: string
  colunasReconhecidas: string[]
  totalLinhas: number
  criados: number
  atualizados: number
  semAlteracao: number
  comErro: number
  estoquesAjustados: number
  categoriasCriadas: string[]
  marcasCriadas: string[]
  linhas: LinhaImportacaoCatalogo[]
}
