import type { Disponibilidade, StatusImportacao, StatusOrcamento } from '../api/tipos'

const moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const numero = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })

export const dinheiro = (valor: number) => moeda.format(valor)
export const quantidade = (valor: number) => numero.format(valor)

/** "2026-12-02" (DateOnly) ou "2026-11-25T21:31:57Z" → "02/12/2026". */
export function data(valor: string) {
  const [ano, mes, dia] = valor.slice(0, 10).split('-')
  return `${dia}/${mes}/${ano}`
}

/** "2026-11-26T01:15:00Z" (UTC, como vem da API) → "25/11/2026 22:15" no horário do computador da loja. */
export function dataHora(valor: string) {
  return new Date(valor).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).replace(',', '')
}

type Cor = 'default' | 'primary' | 'success' | 'warning' | 'error' | 'info'

export const statusOrcamento: Record<StatusOrcamento, { texto: string; cor: Cor }> = {
  EmElaboracao: { texto: 'Em elaboração', cor: 'default' },
  AguardandoAprovacao: { texto: 'Enviado ao cliente', cor: 'info' },
  Aprovado: { texto: 'Aprovado', cor: 'success' },
  Reprovado: { texto: 'Reprovado', cor: 'error' },
  Finalizado: { texto: 'Venda finalizada', cor: 'primary' },
}

export const statusLista: Record<StatusImportacao, { texto: string; cor: Cor }> = {
  Processando: { texto: 'Processando', cor: 'default' },
  AguardandoRevisao: { texto: 'Aguardando revisão', cor: 'warning' },
  Revisada: { texto: 'Revisada', cor: 'success' },
  Erro: { texto: 'Erro', cor: 'error' },
}

export const disponibilidade: Record<Disponibilidade, { texto: string; cor: Cor }> = {
  NaoControlado: { texto: 'Sem controle', cor: 'default' },
  Disponivel: { texto: 'Em estoque', cor: 'success' },
  Parcial: { texto: 'Estoque parcial', cor: 'warning' },
  Indisponivel: { texto: 'Sem estoque', cor: 'error' },
}
