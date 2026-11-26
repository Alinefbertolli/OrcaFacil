import type { Endereco } from '../api/tipos'

/** "12,50", "12.50" ou "1.234,56" → número. Vazio → null. Texto inválido → NaN (a API devolve o erro). */
export function paraNumero(texto: string): number | null {
  let t = texto.trim()
  if (t === '') return null
  // Padrão brasileiro: com vírgula, os pontos são de milhar ("1.234,56") e a vírgula é a decimal.
  if (t.includes(',')) t = t.replace(/\./g, '').replace(',', '.')
  return Number(t)
}

/** Número para mostrar num campo de texto (12.5 → "12,5"; null → ""). */
export const numeroParaTexto = (valor: number | null | undefined) =>
  valor === null || valor === undefined ? '' : String(valor).replace('.', ',')

/** Campo vazio vira null (a API diferencia "não informado" de texto vazio). */
export const textoOuNulo = (texto: string) => (texto.trim() === '' ? null : texto.trim())

export type EnderecoForm = Record<keyof Endereco, string>

export const enderecoVazio: EnderecoForm = {
  logradouro: '', numero: '', complemento: '', bairro: '', cidade: '', uf: '', cep: '',
}

export function enderecoParaForm(e: Endereco | null): EnderecoForm {
  if (!e) return { ...enderecoVazio }
  return {
    logradouro: e.logradouro ?? '', numero: e.numero ?? '', complemento: e.complemento ?? '', bairro: e.bairro ?? '',
    cidade: e.cidade ?? '', uf: e.uf ?? '', cep: e.cep ?? '',
  }
}

/** Endereço todo em branco vira null (o cadastro fica "sem endereço"). */
export function formParaEndereco(f: EnderecoForm): Endereco | null {
  const e: Endereco = {
    logradouro: textoOuNulo(f.logradouro), numero: textoOuNulo(f.numero), complemento: textoOuNulo(f.complemento),
    bairro: textoOuNulo(f.bairro), cidade: textoOuNulo(f.cidade), uf: textoOuNulo(f.uf), cep: textoOuNulo(f.cep),
  }
  return Object.values(e).every((v) => v === null) ? null : e
}
