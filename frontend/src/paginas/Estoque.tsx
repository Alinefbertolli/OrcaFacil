import AddCircleIcon from '@mui/icons-material/AddCircleOutlineOutlined'
import ChecklistIcon from '@mui/icons-material/Checklist'
import HistoryIcon from '@mui/icons-material/History'
import RemoveCircleIcon from '@mui/icons-material/RemoveCircleOutlineOutlined'
import SettingsIcon from '@mui/icons-material/Settings'
import {
  Alert,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  IconButton,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { estoque } from '../api/endpoints'
import type { EstoqueResumo, ProdutoResumo } from '../api/tipos'
import { usePermissoes } from '../auth/usePermissoes'
import { useAvisos } from '../componentes/avisos/useAvisos'
import { DialogoFormulario } from '../componentes/cadastro/DialogoFormulario'
import { TabelaCadastro } from '../componentes/cadastro/TabelaCadastro'
import { EstadoDaConsulta } from '../componentes/Carregando'
import { SeletorProduto } from '../componentes/Seletores'
import { dataHora, quantidade } from '../util/formato'
import { numeroParaTexto, paraNumero, textoOuNulo } from '../util/formulario'

type Operacao = 'entrada' | 'saida' | 'ajuste' | 'configurar'

const TITULOS: Record<Operacao, string> = {
  entrada: 'Registrar entrada',
  saida: 'Registrar saída',
  ajuste: 'Inventário (ajustar saldo)',
  configurar: 'Mínimo e localização',
}

/** O que a operação faz, para a pessoa entender antes de salvar. */
const EXPLICACOES: Record<Operacao, string> = {
  entrada: 'Compra, devolução de cliente... O saldo aumenta. Na primeira entrada, o produto passa a ter controle de estoque.',
  saida: 'Perda, quebra, uso interno... (as vendas finalizadas pelo orçamento já dão baixa sozinhas).',
  ajuste: 'Informe quanto você CONTOU na prateleira. O sistema registra a diferença no histórico.',
  configurar: 'Abaixo do mínimo, o produto aparece no filtro "Só abaixo do mínimo" (lista de reposição).',
}

export function Estoque() {
  const { ehGerente } = usePermissoes()
  const avisos = useAvisos()
  const clienteQuery = useQueryClient()
  const [somenteAbaixo, setSomenteAbaixo] = useState(false)

  // Operação em andamento: qual, em qual produto e os campos do formulário.
  const [operacao, setOperacao] = useState<Operacao | null>(null)
  const [alvo, setAlvo] = useState<{ produtoId: string; nome: string; unidade: string } | null>(null)
  const [produtoNovo, setProdutoNovo] = useState<ProdutoResumo | null>(null) // "Dar entrada" em produto ainda sem estoque
  const [valor, setValor] = useState('')
  const [texto, setTexto] = useState('')
  const [historico, setHistorico] = useState<EstoqueResumo | null>(null)

  function abrir(op: Operacao, item: EstoqueResumo | null) {
    operar.reset()
    setOperacao(op)
    setProdutoNovo(null)
    setAlvo(item && { produtoId: item.produtoId, nome: `${item.produtoCodigo} · ${item.produtoNome}`, unidade: item.unidadeMedida })
    setValor(op === 'configurar' && item ? numeroParaTexto(item.estoqueMinimo) : op === 'ajuste' && item ? numeroParaTexto(item.quantidadeDisponivel) : '')
    setTexto(op === 'configurar' && item ? (item.localizacao ?? '') : '')
  }

  const operar = useMutation({
    mutationFn: () => {
      const produtoId = alvo?.produtoId ?? produtoNovo?.id
      if (!produtoId) throw new Error('Escolha o produto.')
      const numero = paraNumero(valor) ?? Number.NaN
      switch (operacao) {
        case 'entrada': return estoque.entrada(produtoId, numero, textoOuNulo(texto))
        case 'saida': return estoque.saida(produtoId, numero, textoOuNulo(texto))
        case 'ajuste': return estoque.ajuste(produtoId, numero, texto)
        default: return estoque.configurar(produtoId, numero, textoOuNulo(texto))
      }
    },
    onSuccess: (resposta) => {
      void clienteQuery.invalidateQueries({ queryKey: ['estoque'] })
      setOperacao(null)
      avisos.sucesso(`${resposta.produtoNome}: saldo ${quantidade(resposta.quantidadeDisponivel)} ${resposta.unidadeMedida}.`)
    },
  })

  const botao = (dica: string, icone: ReactNode, aoClicar: () => void) => (
    <Tooltip title={dica}>
      <IconButton size="small" onClick={aoClicar}>{icone}</IconButton>
    </Tooltip>
  )

  return (
    <>
      <TabelaCadastro<EstoqueResumo>
        titulo="Estoque"
        descricao="Produtos com controle de estoque. Os que não aparecem aqui são vendidos sem controle (ex.: sob encomenda)."
        chave="estoque"
        chaveExtra={[somenteAbaixo]}
        buscar={(f) => estoque.listar({ ...f, abaixoDoMinimo: somenteAbaixo })}
        idDe={(e) => e.produtoId}
        aoNovo={() => abrir('entrada', null)}
        textoNovo="Dar entrada em produto"
        filtrosExtras={
          <FormControlLabel label="Só abaixo do mínimo"
            control={<Switch checked={somenteAbaixo} onChange={(e) => setSomenteAbaixo(e.target.checked)} />} />
        }
        textoVazio='Nenhum produto com controle de estoque. Use "Dar entrada em produto" para começar.'
        colunas={[
          { titulo: 'Código', valor: (e) => e.produtoCodigo, largura: 110 },
          { titulo: 'Produto', valor: (e) => e.produtoNome },
          {
            titulo: 'Saldo', alinhar: 'right',
            valor: (e) => (
              <Typography component="span" sx={{ fontWeight: 600 }} color={e.abaixoDoMinimo ? 'error.main' : undefined}>
                {quantidade(e.quantidadeDisponivel)} {e.unidadeMedida}
              </Typography>
            ),
          },
          { titulo: 'Mínimo', alinhar: 'right', valor: (e) => quantidade(e.estoqueMinimo) },
          { titulo: 'Situação', valor: (e) => (e.abaixoDoMinimo ? <Chip size="small" color="error" label="Repor" /> : <Chip size="small" color="success" variant="outlined" label="OK" />) },
          { titulo: 'Localização', valor: (e) => e.localizacao ?? '—' },
        ]}
        acoes={(e) => (
          <>
            {botao('Entrada', <AddCircleIcon fontSize="small" color="success" />, () => abrir('entrada', e))}
            {botao('Saída', <RemoveCircleIcon fontSize="small" color="error" />, () => abrir('saida', e))}
            {ehGerente && botao('Inventário', <ChecklistIcon fontSize="small" />, () => abrir('ajuste', e))}
            {ehGerente && botao('Mínimo e localização', <SettingsIcon fontSize="small" />, () => abrir('configurar', e))}
            {botao('Histórico', <HistoryIcon fontSize="small" />, () => setHistorico(e))}
          </>
        )}
      />

      {operacao && (
        <DialogoFormulario aberto titulo={TITULOS[operacao]} aoFechar={() => setOperacao(null)}
          aoSalvar={() => operar.mutate()} salvando={operar.isPending} erro={operar.error}>
          <Alert severity="info">{EXPLICACOES[operacao]}</Alert>
          {alvo ? (
            <Typography sx={{ fontWeight: 600 }}>{alvo.nome}</Typography>
          ) : (
            <SeletorProduto valor={produtoNovo} aoMudar={setProdutoNovo} />
          )}
          <TextField autoFocus required
            label={operacao === 'ajuste' ? 'Saldo contado' : operacao === 'configurar' ? 'Estoque mínimo' : 'Quantidade'}
            value={valor} onChange={(e) => setValor(e.target.value)}
            helperText={alvo?.unidade ?? produtoNovo?.unidadeMedida}
            slotProps={{ htmlInput: { inputMode: 'decimal' } }} />
          <TextField
            label={operacao === 'configurar' ? 'Localização (opcional)' : operacao === 'ajuste' ? 'Motivo' : 'Motivo (opcional)'}
            required={operacao === 'ajuste'} value={texto} onChange={(e) => setTexto(e.target.value)}
            placeholder={operacao === 'configurar' ? 'Ex.: Prateleira B2' : operacao === 'entrada' ? 'Ex.: Compra NF 123' : ''} />
        </DialogoFormulario>
      )}

      <DialogoHistorico item={historico} aoFechar={() => setHistorico(null)} />
    </>
  )
}

function DialogoHistorico({ item, aoFechar }: { item: EstoqueResumo | null; aoFechar: () => void }) {
  const consulta = useQuery({
    queryKey: ['estoque', 'movimentacoes', item?.produtoId],
    queryFn: () => estoque.movimentacoes(item!.produtoId),
    enabled: item !== null,
  })
  const nomes = { Entrada: 'Entrada', Saida: 'Saída', Ajuste: 'Inventário' } as const

  return (
    <Dialog open={item !== null} onClose={aoFechar} fullWidth maxWidth="md">
      <DialogTitle>Histórico · {item?.produtoNome}</DialogTitle>
      <DialogContent>
        <EstadoDaConsulta carregando={consulta.isPending} erro={consulta.error} />
        {consulta.data && (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Quando</TableCell>
                <TableCell>Tipo</TableCell>
                <TableCell align="right">Quantidade</TableCell>
                <TableCell align="right">Saldo</TableCell>
                <TableCell>Motivo</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.data.itens.map((m) => (
                <TableRow key={m.id}>
                  <TableCell>{dataHora(m.criadoEm)}</TableCell>
                  <TableCell>{nomes[m.tipo]}</TableCell>
                  <TableCell align="right">{m.tipo === 'Saida' ? '−' : m.tipo === 'Entrada' ? '+' : ''}{quantidade(m.quantidade)}</TableCell>
                  <TableCell align="right">{quantidade(m.saldoAnterior)} → {quantidade(m.saldoPosterior)}</TableCell>
                  <TableCell>{m.motivo ?? ''}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={aoFechar}>Fechar</Button>
      </DialogActions>
    </Dialog>
  )
}
