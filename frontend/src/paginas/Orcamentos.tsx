import {
  Box,
  Chip,
  MenuItem,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { orcamentos } from '../api/endpoints'
import type { StatusOrcamento } from '../api/tipos'
import { EstadoDaConsulta } from '../componentes/Carregando'
import { Paginacao } from '../componentes/Paginacao'
import { data, dataHora, statusOrcamento } from '../util/formato'

export function Orcamentos() {
  const navegar = useNavigate()
  const [status, setStatus] = useState<StatusOrcamento | ''>('')
  const [pagina, setPagina] = useState(1)
  const consulta = useQuery({ queryKey: ['orcamentos', status, pagina], queryFn: () => orcamentos.listar(status, pagina) })

  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h5">Orçamentos</Typography>
        <TextField select size="small" label="Situação" sx={{ minWidth: 220 }} value={status}
          onChange={(e) => { setStatus(e.target.value as StatusOrcamento | ''); setPagina(1) }}>
          <MenuItem value="">Todas</MenuItem>
          {(Object.keys(statusOrcamento) as StatusOrcamento[]).map((s) => (
            <MenuItem key={s} value={s}>{statusOrcamento[s].texto}</MenuItem>
          ))}
        </TextField>
      </Stack>
      <EstadoDaConsulta carregando={consulta.isPending} erro={consulta.error} />
      {consulta.data && (
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Número</TableCell>
                <TableCell>Tipo</TableCell>
                <TableCell>Situação</TableCell>
                <TableCell>Criado em</TableCell>
                <TableCell>Válido até</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.data.itens.map((o) => (
                <TableRow key={o.id} hover sx={{ cursor: 'pointer' }} onClick={() => navegar(`/orcamentos/${o.id}`)}>
                  <TableCell sx={{ fontWeight: 600 }}>{o.numero}</TableCell>
                  <TableCell>{o.modo === 'Unico' ? 'Único' : 'Comparativo'}</TableCell>
                  <TableCell>
                    <Chip size="small" label={statusOrcamento[o.status].texto} color={statusOrcamento[o.status].cor} />
                  </TableCell>
                  <TableCell>{dataHora(o.criadoEm)}</TableCell>
                  <TableCell>{data(o.validadeAte)}</TableCell>
                </TableRow>
              ))}
              {consulta.data.itens.length === 0 && (
                <TableRow><TableCell colSpan={5}><Box sx={{ py: 2, textAlign: 'center' }}>Nenhum orçamento encontrado.</Box></TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      {consulta.data && <Paginacao pagina={consulta.data} aoMudar={setPagina} />}
    </Stack>
  )
}
