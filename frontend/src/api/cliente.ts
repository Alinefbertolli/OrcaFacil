import type { RespostaAutenticacao } from './tipos'

/**
 * "Cliente HTTP" do OrçaFácil: todas as telas falam com a API por aqui.
 * Ele cuida de três coisas para as telas não precisarem se preocupar:
 *  1. enviar o token de acesso (o "crachá") em toda requisição;
 *  2. quando o token vence (401), renovar a sessão com o refresh token e repetir a requisição, sem a pessoa perceber;
 *  3. transformar as respostas de erro da API (ProblemDetails) num erro com mensagem amigável.
 */

const PREFIXO = '/api/v1'
const CHAVE_REFRESH = 'orcafacil.refreshToken'

/** Erro vindo da API, já com a mensagem pronta para mostrar na tela. */
export class ErroApi extends Error {
  readonly status: number
  readonly erros: Record<string, string[]>

  constructor(status: number, mensagem: string, erros: Record<string, string[]> = {}) {
    super(mensagem)
    this.name = 'ErroApi'
    this.status = status
    this.erros = erros
  }
}

// O token de acesso fica só na memória (some ao fechar a aba): é o mais seguro.
// O refresh token fica no localStorage para a sessão sobreviver a um F5.
let tokenAcesso: string | null = null
let renovacaoEmAndamento: Promise<RespostaAutenticacao | null> | null = null
let aoExpirarSessao: (() => void) | null = null

export const sessao = {
  iniciar(resposta: RespostaAutenticacao) {
    tokenAcesso = resposta.tokenAcesso
    localStorage.setItem(CHAVE_REFRESH, resposta.refreshToken)
  },
  encerrar() {
    tokenAcesso = null
    localStorage.removeItem(CHAVE_REFRESH)
  },
  refreshToken: (): string | null => localStorage.getItem(CHAVE_REFRESH),
  /** A tela de login é avisada quando a sessão acaba de vez (refresh recusado). */
  quandoExpirar(callback: () => void) {
    aoExpirarSessao = callback
  },
}

/**
 * Renova a sessão. Se várias requisições receberem 401 ao mesmo tempo, só UMA renovação é feita
 * e todas esperam por ela (o refresh token só pode ser usado uma vez: a API detecta reuso).
 */
export function renovarSessao(): Promise<RespostaAutenticacao | null> {
  const refreshToken = sessao.refreshToken()
  if (!refreshToken) return Promise.resolve(null)

  renovacaoEmAndamento ??= (async () => {
    try {
      const resposta = await fetch(`${PREFIXO}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })
      if (!resposta.ok) {
        sessao.encerrar()
        aoExpirarSessao?.()
        return null
      }
      const dados = (await resposta.json()) as RespostaAutenticacao
      sessao.iniciar(dados)
      return dados
    } finally {
      renovacaoEmAndamento = null
    }
  })()
  return renovacaoEmAndamento
}

type Corpo = object | FormData | undefined

async function enviar(metodo: string, caminho: string, corpo: Corpo): Promise<Response> {
  const headers: Record<string, string> = {}
  if (tokenAcesso) headers.Authorization = `Bearer ${tokenAcesso}`

  let body: BodyInit | undefined
  if (corpo instanceof FormData) {
    body = corpo // o navegador monta o "multipart/form-data" sozinho
  } else if (corpo !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(corpo)
  }
  return fetch(`${PREFIXO}${caminho}`, { method: metodo, headers, body })
}

/** Faz a requisição; em 401, renova a sessão uma vez e tenta de novo. */
async function requisitar(metodo: string, caminho: string, corpo?: Corpo): Promise<Response> {
  let resposta = await enviar(metodo, caminho, corpo)

  const ehRotaDeLogin = caminho.startsWith('/auth/')
  if (resposta.status === 401 && !ehRotaDeLogin && sessao.refreshToken()) {
    const renovada = await renovarSessao()
    if (renovada) resposta = await enviar(metodo, caminho, corpo)
  }

  if (!resposta.ok) throw await erroDaResposta(resposta)
  return resposta
}

/** Lê o ProblemDetails da API (title, detail, errors) e monta uma mensagem legível. */
async function erroDaResposta(resposta: Response): Promise<ErroApi> {
  try {
    const problema = (await resposta.json()) as {
      title?: string
      detail?: string
      errors?: Record<string, string[]>
    }
    const errosDeCampo = Object.values(problema.errors ?? {}).flat()
    const mensagem = problema.detail ?? (errosDeCampo.length > 0 ? errosDeCampo.join(' ') : problema.title)
    return new ErroApi(resposta.status, mensagem ?? `Erro ${resposta.status}`, problema.errors)
  } catch {
    return new ErroApi(
      resposta.status,
      resposta.status === 0 || resposta.status >= 502
        ? 'Não foi possível falar com o servidor. A API está ligada?'
        : `Erro ${resposta.status}`,
    )
  }
}

/** Atalhos usados pelas telas. */
export const http = {
  async get<T>(caminho: string): Promise<T> {
    return (await requisitar('GET', caminho)).json() as Promise<T>
  },
  async post<T>(caminho: string, corpo?: Corpo): Promise<T> {
    const resposta = await requisitar('POST', caminho, corpo)
    return (resposta.status === 204 ? undefined : await resposta.json()) as T
  },
  async put<T>(caminho: string, corpo?: Corpo): Promise<T> {
    return (await requisitar('PUT', caminho, corpo)).json() as Promise<T>
  },
  async delete(caminho: string): Promise<void> {
    await requisitar('DELETE', caminho)
  },
  /** Baixa um arquivo (ex.: o PDF) e devolve o conteúdo e o nome sugerido pelo servidor. */
  async arquivo(caminho: string): Promise<{ conteudo: Blob; nome: string }> {
    const resposta = await requisitar('GET', caminho)
    const disposicao = resposta.headers.get('content-disposition') ?? ''
    const nome = /filename="?([^";]+)"?/i.exec(disposicao)?.[1] ?? 'arquivo'
    return { conteudo: await resposta.blob(), nome }
  },
}

/** Mensagem amigável para qualquer erro (da API ou de rede). */
export function mensagemDeErro(erro: unknown): string {
  if (erro instanceof ErroApi) return erro.message
  if (erro instanceof TypeError) return 'Não foi possível falar com o servidor. A API está ligada?'
  if (erro instanceof Error && erro.message) return erro.message // erros criados pelas próprias telas (ex.: "Escolha o produto.")
  return 'Ocorreu um erro inesperado.'
}
