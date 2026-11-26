import { Autocomplete, TextField } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { catalogo } from '../api/endpoints'
import type { ClienteResumo, ProdutoResumo } from '../api/tipos'
import { dinheiro } from '../util/formato'
import { useValorAtrasado } from '../util/useValorAtrasado'

export function SeletorProduto({
  valor,
  aoMudar,
  rotulo = 'Produto do catálogo',
}: {
  valor: ProdutoResumo | null
  aoMudar: (produto: ProdutoResumo | null) => void
  rotulo?: string
}) {
  const [texto, setTexto] = useState('')
  const busca = useValorAtrasado(texto)
  const consulta = useQuery({
    queryKey: ['produtos', 'busca', busca],
    queryFn: () => catalogo.buscarProdutos(busca),
    enabled: busca.trim().length >= 2,
  })

  return (
    <Autocomplete
      size="small"
      options={consulta.data?.itens ?? (valor ? [valor] : [])}
      value={valor}
      onChange={(_, novo) => aoMudar(novo)}
      onInputChange={(_, novoTexto) => setTexto(novoTexto)}
      getOptionLabel={(p) => `${p.codigo} · ${p.nome}`}
      isOptionEqualToValue={(a, b) => a.id === b.id}
      filterOptions={(x) => x} // quem filtra é a API
      loading={consulta.isFetching}
      noOptionsText={texto.trim().length < 2 ? 'Digite ao menos 2 letras' : 'Nenhum produto encontrado'}
      renderOption={({ key, ...props }, p) => (
        <li key={key} {...props}>
          {p.codigo} · {p.nome}
          {p.marcaNome ? ` (${p.marcaNome})` : ''} — {p.precoVenda == null ? 'sem preço' : dinheiro(p.precoVenda)}
        </li>
      )}
      renderInput={(params) => <TextField {...params} label={rotulo} />}
    />
  )
}

export function SeletorCliente({
  valor,
  aoMudar,
}: {
  valor: ClienteResumo | null
  aoMudar: (cliente: ClienteResumo | null) => void
}) {
  const [texto, setTexto] = useState('')
  const busca = useValorAtrasado(texto)
  const consulta = useQuery({
    queryKey: ['clientes', 'busca', busca],
    queryFn: () => catalogo.buscarClientes(busca),
    enabled: busca.trim().length >= 2,
  })

  return (
    <Autocomplete
      options={consulta.data?.itens ?? (valor ? [valor] : [])}
      value={valor}
      onChange={(_, novo) => aoMudar(novo)}
      onInputChange={(_, novoTexto) => setTexto(novoTexto)}
      getOptionLabel={(c) => (c.telefone ? `${c.nome} · ${c.telefone}` : c.nome)}
      isOptionEqualToValue={(a, b) => a.id === b.id}
      filterOptions={(x) => x}
      loading={consulta.isFetching}
      noOptionsText={texto.trim().length < 2 ? 'Digite ao menos 2 letras' : 'Nenhum cliente encontrado'}
      renderInput={(params) => <TextField {...params} label="Cliente (opcional)" />}
    />
  )
}
