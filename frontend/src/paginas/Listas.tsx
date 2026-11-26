import { Box, Button, Chip, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link as LinkRouter, useNavigate } from 'react-router'
import { importacoes } from '../api/endpoints'
import { EstadoDaConsulta } from '../componentes/Carregando'
import { Paginacao } from '../componentes/Paginacao'
import { dataHora, statusLista } from '../util/formato'

export function Listas() {
  const navegar = useNavigate()
  const [pagina, setPagina] = useState(1)
  const consulta = useQuery({ queryKey: ['listas', pagina], queryFn: () => importacoes.listar(pagina) })

  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h5">Listas recebidas</Typography>
        <Button variant="contained" component={LinkRouter} to="/listas/nova">Nova lista</Button>
      </Stack>
      <EstadoDaConsulta carregando={consulta.isPending} erro={consulta.error} />
      {consulta.data && (
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Recebida em</TableCell>
                <TableCell>Origem</TableCell>
                <TableCell>Situação</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.data.itens.map((l) => (
                <TableRow key={l.id} hover sx={{ cursor: 'pointer' }} onClick={() => navegar(`/listas/${l.id}`)}>
                  <TableCell>{dataHora(l.criadoEm)}</TableCell>
                  <TableCell>{l.nomeArquivoOriginal ?? l.formato}</TableCell>
                  <TableCell>
                    <Chip size="small" label={statusLista[l.status].texto} color={statusLista[l.status].cor} />
                  </TableCell>
                </TableRow>
              ))}
              {consulta.data.itens.length === 0 && (
                <TableRow><TableCell colSpan={3}><Box sx={{ py: 2, textAlign: 'center' }}>Nenhuma lista ainda.</Box></TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      {consulta.data && <Paginacao pagina={consulta.data} aoMudar={setPagina} />}
    </Stack>
  )
}
