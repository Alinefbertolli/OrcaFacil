import AddIcon from '@mui/icons-material/Add'
import DeleteIcon from '@mui/icons-material/DeleteOutlineOutlined'
import SaveIcon from '@mui/icons-material/Save'
import WarningIcon from '@mui/icons-material/WarningAmber'
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  FormControlLabel,
  FormGroup,
  IconButton,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from '@mui/material'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type KeyboardEvent } from 'react'
import { useNavigate, useParams } from 'react-router'
import { mensagemDeErro } from '../api/cliente'
import { importacoes, orcamentos } from '../api/endpoints'
import type { ItemImportado, ListaImportada, ModoOrcamento, ProdutoResumo, TipoOpcao } from '../api/tipos'
import { EstadoDaConsulta } from '../componentes/Carregando'
import { SeletorProduto } from '../componentes/Seletores'
import { dataHora, quantidade, statusLista } from '../util/formato'

/** O seletor de produto trabalha com ProdutoResumo; aqui montamos um a partir do que a lista já sabe. */
function produtoDoItem(item: ItemImportado): ProdutoResumo | null {
  if (!item.produtoId) return null
  return {
    id: item.produtoId, codigo: item.produtoCodigo ?? '', nome: item.produtoNome ?? '', unidadeMedida: item.unidadeMedida,
    categoriaId: '', categoriaNome: null, marcaId: null, marcaNome: null, precoVenda: null, grupoEquivalencia: null, ativo: true,
  }
}

/** Passo 2 do fluxo: conferir e corrigir o que o sistema entendeu da lista, e então gerar o orçamento. */
export function RevisaoLista() {
  const { id = '' } = useParams()
  const clienteQuery = useQueryClient()
  const consulta = useQuery({ queryKey: ['lista', id], queryFn: () => importacoes.obter(id) })
  const lista = consulta.data

  const atualizarCache = (nova: ListaImportada) => clienteQuery.setQueryData(['lista', id], nova)

  const confirmar = useMutation({
    mutationFn: () => importacoes.confirmarRevisao(id),
    onSuccess: atualizarCache,
  })

  if (!lista) return <EstadoDaConsulta carregando={consulta.isPending} erro={consulta.error} />

  const emRevisao = lista.status === 'AguardandoRevisao'

  return (
    <Stack spacing={2}>
      <Box>
        <Typography variant="h5">Revisão da lista</Typography>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mt: 0.5 }}>
          <Chip size="small" label={statusLista[lista.status].texto} color={statusLista[lista.status].cor} />
          <Typography variant="body2" color="text.secondary">
            {lista.nomeArquivoOriginal ?? lista.formato} · recebida em {dataHora(lista.criadoEm)} · {lista.totalItens} itens
          </Typography>
        </Stack>
      </Box>

      {lista.status === 'Erro' && <Alert severity="error">{lista.mensagemErro}</Alert>}
      {emRevisao && lista.itensQuePrecisamAtencao > 0 && (
        <Alert severity="warning">
          {lista.itensQuePrecisamAtencao} item(ns) precisam de atenção: confira a descrição e escolha o produto do catálogo.
        </Alert>
      )}

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell width={36}>#</TableCell>
              <TableCell>Descrição</TableCell>
              <TableCell width={90}>Qtd</TableCell>
              <TableCell width={80}>Un</TableCell>
              <TableCell width={130}>Marca pedida</TableCell>
              <TableCell sx={{ minWidth: 280 }}>Produto do catálogo</TableCell>
              {emRevisao && <TableCell width={90} />}
            </TableRow>
          </TableHead>
          <TableBody>
            {lista.itens.map((item) =>
              emRevisao ? (
                <LinhaEditavel key={item.id} listaId={id} item={item} aoSalvar={atualizarCache} />
              ) : (
                <TableRow key={item.id}>
                  <TableCell>{item.ordem}</TableCell>
                  <TableCell>{item.descricao}</TableCell>
                  <TableCell>{quantidade(item.quantidade)}</TableCell>
                  <TableCell>{item.unidadeMedida}</TableCell>
                  <TableCell>{item.marca ?? '—'}</TableCell>
                  <TableCell>{item.produtoId ? `${item.produtoCodigo} · ${item.produtoNome}` : 'Sem produto (ficará "sem preço")'}</TableCell>
                </TableRow>
              ),
            )}
            {emRevisao && <LinhaNova listaId={id} aoSalvar={atualizarCache} />}
          </TableBody>
        </Table>
      </TableContainer>

      {emRevisao && (
        <Stack spacing={1} sx={{ alignItems: 'flex-start' }}>
          {confirmar.isError && <Alert severity="error">{mensagemDeErro(confirmar.error)}</Alert>}
          <Button variant="contained" size="large" onClick={() => confirmar.mutate()} disabled={confirmar.isPending}>
            Confirmar revisão
          </Button>
          <Typography variant="caption" color="text.secondary">
            Depois de confirmada, a lista não pode mais ser alterada e o orçamento pode ser gerado.
          </Typography>
        </Stack>
      )}

      {lista.status === 'Revisada' && <GerarOrcamento lista={lista} />}
    </Stack>
  )
}

/**
 * Enter digitado num campo de texto comum. Ignora o Enter da busca de produto: lá ele serve para
 * ESCOLHER o produto da lista (o MUI marca esse evento com preventDefault), e salvar junto gravaria o valor antigo.
 */
function enterDeCampoDeTexto(e: KeyboardEvent) {
  const dentroDaBusca = (e.target as HTMLElement).closest('.MuiAutocomplete-root') !== null
  return e.key === 'Enter' && !e.defaultPrevented && !dentroDaBusca
}

function LinhaEditavel({ listaId, item, aoSalvar }: { listaId: string; item: ItemImportado; aoSalvar: (l: ListaImportada) => void }) {
  const [descricao, setDescricao] = useState(item.descricao)
  const [qtd, setQtd] = useState(String(item.quantidade))
  const [unidade, setUnidade] = useState(item.unidadeMedida)
  const [marca, setMarca] = useState(item.marca ?? '')
  const [produto, setProduto] = useState<ProdutoResumo | null>(produtoDoItem(item))

  const alterado =
    descricao !== item.descricao || Number(qtd.replace(',', '.')) !== item.quantidade || unidade !== item.unidadeMedida ||
    (marca || null) !== item.marca || (produto?.id ?? null) !== item.produtoId

  const salvar = useMutation({
    mutationFn: () =>
      importacoes.atualizarItem(listaId, item.id, {
        descricao, quantidade: Number(qtd.replace(',', '.')), unidadeMedida: unidade, marca: marca || null, produtoId: produto?.id ?? null,
      }),
    onSuccess: aoSalvar,
  })
  const excluir = useMutation({
    mutationFn: async () => {
      await importacoes.excluirItem(listaId, item.id)
      return importacoes.obter(listaId)
    },
    onSuccess: aoSalvar,
  })

  const erro = salvar.error ?? excluir.error
  // Enter em qualquer campo da linha salva (como clicar no disquete).
  const salvarComEnter = (e: KeyboardEvent) => {
    if (enterDeCampoDeTexto(e) && alterado && !salvar.isPending) salvar.mutate()
  }
  return (
    <TableRow sx={item.precisaAtencao ? { bgcolor: '#FFF8E1' } : undefined} onKeyDown={salvarComEnter}>
      <TableCell>
        {item.precisaAtencao ? (
          <Tooltip title={`Precisa de atenção (confiança ${Math.round(item.confianca * 100)}%). Texto original: ${item.textoOriginal ?? ''}`}>
            <WarningIcon color="warning" fontSize="small" />
          </Tooltip>
        ) : (
          item.ordem
        )}
      </TableCell>
      <TableCell>
        <TextField size="small" fullWidth value={descricao} onChange={(e) => setDescricao(e.target.value)}
          error={Boolean(erro)} helperText={erro ? mensagemDeErro(erro) : undefined} />
      </TableCell>
      <TableCell>
        <TextField size="small" value={qtd} onChange={(e) => setQtd(e.target.value)} slotProps={{ htmlInput: { inputMode: 'decimal' } }} />
      </TableCell>
      <TableCell>
        <TextField size="small" value={unidade} onChange={(e) => setUnidade(e.target.value.toUpperCase())} />
      </TableCell>
      <TableCell>
        <TextField size="small" value={marca} onChange={(e) => setMarca(e.target.value)} />
      </TableCell>
      <TableCell>
        <SeletorProduto valor={produto} aoMudar={setProduto} rotulo="Buscar produto" />
      </TableCell>
      <TableCell sx={{ whiteSpace: 'nowrap' }}>
        <Tooltip title="Salvar alterações">
          <span>
            <IconButton color="primary" disabled={!alterado || salvar.isPending} onClick={() => salvar.mutate()}>
              <SaveIcon />
            </IconButton>
          </span>
        </Tooltip>
        <Tooltip title="Excluir item">
          <IconButton disabled={excluir.isPending} onClick={() => excluir.mutate()}>
            <DeleteIcon />
          </IconButton>
        </Tooltip>
      </TableCell>
    </TableRow>
  )
}

function LinhaNova({ listaId, aoSalvar }: { listaId: string; aoSalvar: (l: ListaImportada) => void }) {
  const [descricao, setDescricao] = useState('')
  const [qtd, setQtd] = useState('1')
  const [unidade, setUnidade] = useState('UN')
  const [produto, setProduto] = useState<ProdutoResumo | null>(null)

  const adicionar = useMutation({
    mutationFn: () =>
      importacoes.adicionarItem(listaId, {
        descricao, quantidade: Number(qtd.replace(',', '.')), unidadeMedida: unidade, marca: null, produtoId: produto?.id ?? null,
      }),
    onSuccess: (lista) => {
      aoSalvar(lista)
      setDescricao('')
      setQtd('1')
      setProduto(null)
    },
  })

  const adicionarComEnter = (e: KeyboardEvent) => {
    if (enterDeCampoDeTexto(e) && descricao.trim() && !adicionar.isPending) adicionar.mutate()
  }

  return (
    <TableRow onKeyDown={adicionarComEnter}>
      <TableCell>
        <AddIcon fontSize="small" color="action" />
      </TableCell>
      <TableCell>
        <TextField size="small" fullWidth placeholder="Adicionar item que faltou" value={descricao}
          onChange={(e) => setDescricao(e.target.value)}
          error={adicionar.isError} helperText={adicionar.isError ? mensagemDeErro(adicionar.error) : undefined} />
      </TableCell>
      <TableCell>
        <TextField size="small" value={qtd} onChange={(e) => setQtd(e.target.value)} />
      </TableCell>
      <TableCell>
        <TextField size="small" value={unidade} onChange={(e) => setUnidade(e.target.value.toUpperCase())} />
      </TableCell>
      <TableCell />
      <TableCell>
        <SeletorProduto valor={produto} aoMudar={setProduto} rotulo="Buscar produto" />
      </TableCell>
      <TableCell>
        <Button size="small" disabled={!descricao.trim() || adicionar.isPending} onClick={() => adicionar.mutate()}>
          Adicionar
        </Button>
      </TableCell>
    </TableRow>
  )
}

const OPCOES: { tipo: TipoOpcao; nome: string; explicacao: string }[] = [
  { tipo: 'MenorPreco', nome: 'Econômica', explicacao: 'o menor preço' },
  { tipo: 'CustoBeneficio', nome: 'Custo-benefício', explicacao: 'o mais barato entre marcas de nível 3 ou mais' },
  { tipo: 'MarcasReconhecidas', nome: 'Marcas reconhecidas', explicacao: 'a marca mais reconhecida' },
]

/** Passo 3: escolher o modo e gerar o orçamento. */
function GerarOrcamento({ lista }: { lista: ListaImportada }) {
  const navegar = useNavigate()
  const [modo, setModo] = useState<ModoOrcamento | 'Padrao'>('Padrao')
  const [opcoes, setOpcoes] = useState<TipoOpcao[]>(OPCOES.map((o) => o.tipo))
  const [validade, setValidade] = useState('7')

  const gerar = useMutation({
    mutationFn: () =>
      orcamentos.gerar({
        listaImportadaId: lista.id,
        modo: modo === 'Padrao' ? undefined : modo,
        opcoes: modo === 'Comparativo' ? opcoes : undefined,
        validadeDias: Number(validade),
      }),
    onSuccess: (orcamento) => navegar(`/orcamentos/${orcamento.id}`),
  })

  const alternar = (tipo: TipoOpcao) =>
    setOpcoes((atuais) => (atuais.includes(tipo) ? atuais.filter((t) => t !== tipo) : [...atuais, tipo]))

  return (
    <Paper sx={{ p: 2 }}>
      <Stack spacing={2}>
        <Typography variant="h6">Gerar orçamento</Typography>
        <ToggleButtonGroup exclusive size="small" value={modo} onChange={(_, novo: ModoOrcamento | 'Padrao' | null) => novo && setModo(novo)}>
          <ToggleButton value="Padrao">Padrão da loja</ToggleButton>
          <ToggleButton value="Unico">Único (itens exatos)</ToggleButton>
          <ToggleButton value="Comparativo">Comparativo (até 3 opções)</ToggleButton>
        </ToggleButtonGroup>

        {modo === 'Comparativo' && (
          <FormGroup>
            {OPCOES.map((o) => (
              <FormControlLabel key={o.tipo} control={<Checkbox checked={opcoes.includes(o.tipo)} onChange={() => alternar(o.tipo)} />}
                label={`${o.nome}: ${o.explicacao}`} />
            ))}
          </FormGroup>
        )}

        <TextField label="Validade (dias)" type="number" size="small" sx={{ width: 160 }} value={validade}
          onChange={(e) => setValidade(e.target.value)} slotProps={{ htmlInput: { min: 1, max: 90 } }} />

        {gerar.isError && <Alert severity="error">{mensagemDeErro(gerar.error)}</Alert>}
        <Box>
          <Button variant="contained" size="large" onClick={() => gerar.mutate()}
            disabled={gerar.isPending || (modo === 'Comparativo' && opcoes.length === 0)}>
            {gerar.isPending ? 'Gerando...' : 'Gerar orçamento'}
          </Button>
        </Box>
      </Stack>
    </Paper>
  )
}
