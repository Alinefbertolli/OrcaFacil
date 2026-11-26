import AddIcon from '@mui/icons-material/Add'
import SearchIcon from '@mui/icons-material/Search'
import {
  Box,
  Button,
  FormControlLabel,
  InputAdornment,
  Paper,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import type { Pagina } from '../../api/tipos'
import { useValorAtrasado } from '../../util/useValorAtrasado'
import { EstadoDaConsulta } from '../Carregando'
import { Paginacao } from '../Paginacao'

export interface Coluna<T> {
  titulo: string
  valor: (item: T) => ReactNode
  alinhar?: 'left' | 'right' | 'center'
  largura?: number | string
}

export interface FiltroTabela {
  pagina: number
  busca: string
  incluirInativos: boolean
}

/**
 * A "moldura" de todas as telas de cadastro: título, busca, botão "Novo", tabela e paginação.
 * Cada tela só diz QUAIS colunas mostrar e COMO buscar; o resto é igual para todas.
 */
export function TabelaCadastro<T>(props: {
  titulo: string
  descricao?: string
  /** Nome do cache (ex.: "marcas"): ao salvar, a tela invalida esse nome e a tabela recarrega. */
  chave: string
  buscar: (filtro: FiltroTabela) => Promise<Pagina<T>>
  colunas: Coluna<T>[]
  idDe: (item: T) => string
  aoNovo?: () => void
  textoNovo?: string
  aoClicar?: (item: T) => void
  acoes?: (item: T) => ReactNode
  comFiltroInativos?: boolean
  /** Filtros a mais (ex.: "só abaixo do mínimo"); entram na chave do cache. */
  filtrosExtras?: ReactNode
  chaveExtra?: unknown[]
  textoVazio?: string
}) {
  const [pagina, setPagina] = useState(1)
  const [texto, setTexto] = useState('')
  const [incluirInativos, setIncluirInativos] = useState(false)
  const busca = useValorAtrasado(texto)

  const consulta = useQuery({
    queryKey: [props.chave, pagina, busca, incluirInativos, ...(props.chaveExtra ?? [])],
    queryFn: () => props.buscar({ pagina, busca, incluirInativos }),
    placeholderData: keepPreviousData, // mantém a página anterior na tela enquanto a nova carrega
  })

  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'flex-start', gap: 2, flexWrap: 'wrap' }}>
        <Box>
          <Typography variant="h5">{props.titulo}</Typography>
          {props.descricao && <Typography color="text.secondary">{props.descricao}</Typography>}
        </Box>
        {props.aoNovo && (
          <Button variant="contained" startIcon={<AddIcon />} onClick={props.aoNovo}>
            {props.textoNovo ?? 'Novo'}
          </Button>
        )}
      </Stack>

      <Stack direction="row" spacing={2} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <TextField size="small" placeholder="Buscar..." value={texto} sx={{ minWidth: 280 }}
          onChange={(e) => { setTexto(e.target.value); setPagina(1) }}
          slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> } }} />
        {props.comFiltroInativos && (
          <FormControlLabel label="Mostrar inativos"
            control={<Switch checked={incluirInativos} onChange={(e) => { setIncluirInativos(e.target.checked); setPagina(1) }} />} />
        )}
        {props.filtrosExtras}
      </Stack>

      <EstadoDaConsulta carregando={consulta.isPending} erro={consulta.error} />
      {consulta.data && (
        <TableContainer component={Paper} sx={{ opacity: consulta.isPlaceholderData ? 0.6 : 1 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                {props.colunas.map((c) => (
                  <TableCell key={c.titulo} align={c.alinhar} sx={{ width: c.largura, fontWeight: 600 }}>{c.titulo}</TableCell>
                ))}
                {props.acoes && <TableCell align="right" />}
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.data.itens.map((item) => (
                <TableRow key={props.idDe(item)} hover
                  sx={props.aoClicar ? { cursor: 'pointer' } : undefined}
                  onClick={props.aoClicar ? () => props.aoClicar?.(item) : undefined}>
                  {props.colunas.map((c) => (
                    <TableCell key={c.titulo} align={c.alinhar}>{c.valor(item)}</TableCell>
                  ))}
                  {props.acoes && (
                    // stopPropagation: clicar num botão da linha não abre a edição da linha
                    <TableCell align="right" sx={{ whiteSpace: 'nowrap' }} onClick={(e) => e.stopPropagation()}>
                      {props.acoes(item)}
                    </TableCell>
                  )}
                </TableRow>
              ))}
              {consulta.data.itens.length === 0 && (
                <TableRow>
                  <TableCell colSpan={props.colunas.length + (props.acoes ? 1 : 0)}>
                    <Box sx={{ py: 3, textAlign: 'center', color: 'text.secondary' }}>
                      {busca ? 'Nada encontrado para essa busca.' : (props.textoVazio ?? 'Nenhum registro ainda.')}
                    </Box>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      {consulta.data && <Paginacao pagina={consulta.data} aoMudar={setPagina} />}
    </Stack>
  )
}
