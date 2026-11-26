import CheckIcon from '@mui/icons-material/CheckCircle'
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdf'
import WhatsAppIcon from '@mui/icons-material/WhatsApp'
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  Paper,
  Snackbar,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useParams } from 'react-router'
import { mensagemDeErro } from '../api/cliente'
import { orcamentos } from '../api/endpoints'
import type { ColunaComparacao, ItemOrcamento, OpcaoOrcamento, Orcamento, StatusOrcamento } from '../api/tipos'
import { EstadoDaConsulta } from '../componentes/Carregando'
import { baixarArquivo } from '../util/baixarArquivo'
import { data, dataHora, dinheiro, disponibilidade, quantidade, statusOrcamento } from '../util/formato'

/** Os botões de situação que fazem sentido em cada momento (a API confere as regras de novo). */
const ACOES: Record<StatusOrcamento, { para: StatusOrcamento; texto: string; cor: 'primary' | 'success' | 'error' | 'inherit' }[]> = {
  EmElaboracao: [{ para: 'AguardandoAprovacao', texto: 'Marcar como enviado ao cliente', cor: 'primary' }],
  AguardandoAprovacao: [
    { para: 'Aprovado', texto: 'Cliente aprovou', cor: 'success' },
    { para: 'Reprovado', texto: 'Cliente recusou', cor: 'error' },
    { para: 'EmElaboracao', texto: 'Voltar para edição', cor: 'inherit' },
  ],
  Aprovado: [{ para: 'Finalizado', texto: 'Finalizar venda (baixa no estoque)', cor: 'primary' }],
  Reprovado: [{ para: 'EmElaboracao', texto: 'Voltar para edição', cor: 'inherit' }],
  Finalizado: [],
}

/** Passo 4 do fluxo: comparar as opções, completar preços, enviar ao cliente (PDF/WhatsApp) e acompanhar. */
export function OrcamentoDetalhe() {
  const { id = '' } = useParams()
  const clienteQuery = useQueryClient()
  const consulta = useQuery({ queryKey: ['orcamento', id], queryFn: () => orcamentos.obter(id) })
  const orcamento = consulta.data
  const comparativo = (orcamento?.opcoes.length ?? 0) > 1
  const comparacao = useQuery({
    queryKey: ['comparacao', id],
    queryFn: () => orcamentos.comparar(id),
    enabled: comparativo,
  })

  const [abaOpcao, setAbaOpcao] = useState(0)
  const [whatsAberto, setWhatsAberto] = useState(false)
  const [erroVisivel, setErroVisivel] = useState<string | null>(null)

  // Toda ação devolve o orçamento atualizado: guardamos no cache e recalculamos a comparação.
  const atualizar = (novo: Orcamento) => {
    clienteQuery.setQueryData(['orcamento', id], novo)
    void clienteQuery.invalidateQueries({ queryKey: ['comparacao', id] })
    void clienteQuery.invalidateQueries({ queryKey: ['orcamentos'] })
  }

  const selecionar = useMutation({
    mutationFn: (opcaoId: string) => orcamentos.selecionar(id, opcaoId),
    onSuccess: atualizar,
    onError: (e) => setErroVisivel(mensagemDeErro(e)),
  })
  const mudarStatus = useMutation({
    mutationFn: (status: StatusOrcamento) => orcamentos.alterarStatus(id, status),
    onSuccess: atualizar,
    onError: (e) => setErroVisivel(mensagemDeErro(e)),
  })
  const baixarPdf = useMutation({
    mutationFn: () => orcamentos.pdf(id),
    onSuccess: ({ conteudo, nome }) => baixarArquivo(conteudo, nome),
    onError: (e) => setErroVisivel(mensagemDeErro(e)),
  })

  if (!orcamento) return <EstadoDaConsulta carregando={consulta.isPending} erro={consulta.error} />

  const editavel = orcamento.status === 'EmElaboracao'
  const colunaDe = (opcao: OpcaoOrcamento) => comparacao.data?.opcoes.find((c) => c.opcaoId === opcao.id)
  const opcaoAtual = orcamento.opcoes[Math.min(abaOpcao, orcamento.opcoes.length - 1)]

  // O que vai para o cliente: antes da escolha, todas as opções; depois, só a escolhida (regra da Fase 9).
  const selecionada = orcamento.opcoes.find((o) => o.selecionada)
  const paraEnviar = selecionada ? [selecionada] : orcamento.opcoes
  const pendentes = paraEnviar.filter((o) => o.itensSemPreco > 0)
  const motivoBloqueio =
    pendentes.length > 0
      ? `Informe os preços que faltam em ${pendentes.map((o) => `"${o.nome}" (${o.itensSemPreco})`).join(', ')} para enviar ao cliente.`
      : null

  // Mostra o erro de qualquer ação num aviso fixo no rodapé (sempre à vista, mesmo com a página rolada).
  const aoErrar = (erro: unknown) => setErroVisivel(mensagemDeErro(erro))

  return (
    <Stack spacing={2}>
      {/* Cabeçalho */}
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 2 }}>
        <Box>
          <Typography variant="h5">Orçamento {orcamento.numero}</Typography>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mt: 0.5, flexWrap: 'wrap' }}>
            <Chip size="small" label={statusOrcamento[orcamento.status].texto} color={statusOrcamento[orcamento.status].cor} />
            <Typography variant="body2" color="text.secondary">
              {orcamento.modo === 'Unico' ? 'Único' : 'Comparativo'} · {orcamento.clienteNome ?? 'sem cliente'} · criado em{' '}
              {dataHora(orcamento.criadoEm)} · válido até {data(orcamento.validadeAte)}
            </Typography>
          </Stack>
        </Box>
        {/* Botões desabilitados explicam o motivo ao passar o mouse (o <span> é necessário para o Tooltip funcionar) */}
        <Tooltip title={motivoBloqueio ?? ''}>
          <Stack direction="row" spacing={1} component="span">
            <Button variant="outlined" startIcon={<PictureAsPdfIcon />} onClick={() => baixarPdf.mutate()}
              disabled={baixarPdf.isPending || motivoBloqueio !== null}>
              {baixarPdf.isPending ? 'Gerando...' : 'Baixar PDF'}
            </Button>
            <Button variant="contained" color="success" startIcon={<WhatsAppIcon />} onClick={() => setWhatsAberto(true)}
              disabled={motivoBloqueio !== null}>
              WhatsApp
            </Button>
          </Stack>
        </Tooltip>
      </Stack>

      {motivoBloqueio && orcamento.status === 'EmElaboracao' && (
        <Alert severity="info">
          {motivoBloqueio} Dica: o preço informado num item vale para o mesmo item em todas as opções.
        </Alert>
      )}
      {orcamento.avisos.map((aviso) => (
        <Alert key={aviso} severity="warning">{aviso}</Alert>
      ))}

      {/* Cartões das opções, lado a lado */}
      <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 2 }}>
        {orcamento.opcoes.map((opcao, indice) => (
          <CartaoOpcao
            key={opcao.id}
            opcao={opcao}
            coluna={colunaDe(opcao)}
            ativo={opcao.id === opcaoAtual.id}
            podeEscolher={editavel && !opcao.selecionada}
            aoVer={() => setAbaOpcao(indice)}
            aoEscolher={() => selecionar.mutate(opcao.id)}
          />
        ))}
      </Stack>

      {/* Itens da opção escolhida na aba */}
      <Paper>
        {orcamento.opcoes.length > 1 && (
          <Tabs value={orcamento.opcoes.indexOf(opcaoAtual)} onChange={(_, i: number) => setAbaOpcao(i)} sx={{ px: 1 }}>
            {orcamento.opcoes.map((o) => (
              <Tab key={o.id} label={o.nome} />
            ))}
          </Tabs>
        )}
        <TabelaItens orcamento={orcamento} opcao={opcaoAtual} editavel={editavel} aoAtualizar={atualizar} aoErrar={aoErrar} />
      </Paper>

      {/* Situação */}
      {ACOES[orcamento.status].length > 0 && (
        <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }}>
          {ACOES[orcamento.status].map((acao) => {
            // Enviar ao cliente exige uma opção escolhida e sem itens sem preço: avisamos antes de clicar.
            const bloqueio =
              acao.para !== 'AguardandoAprovacao' ? null
                : !selecionada ? 'Primeiro marque qual opção o cliente escolheu ("Cliente escolheu esta").'
                : motivoBloqueio
            return (
              <Tooltip key={acao.para} title={bloqueio ?? ''}>
                <span>
                  <Button variant={acao.cor === 'inherit' ? 'text' : 'contained'} color={acao.cor}
                    disabled={mudarStatus.isPending || bloqueio !== null} onClick={() => mudarStatus.mutate(acao.para)}>
                    {acao.texto}
                  </Button>
                </span>
              </Tooltip>
            )
          })}
        </Stack>
      )}
      {orcamento.economia > 0 && (
        <Typography variant="body2" color="success.main">
          O cliente economizou {dinheiro(orcamento.economia)} em relação à opção mais cara.
        </Typography>
      )}

      <DialogoWhatsApp orcamentoId={id} aberto={whatsAberto} aoFechar={() => setWhatsAberto(false)} />

      <Snackbar open={erroVisivel !== null} autoHideDuration={8000} onClose={() => setErroVisivel(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}>
        <Alert severity="error" variant="filled" onClose={() => setErroVisivel(null)}>{erroVisivel}</Alert>
      </Snackbar>
    </Stack>
  )
}

function CartaoOpcao(props: {
  opcao: OpcaoOrcamento
  coluna: ColunaComparacao | undefined
  ativo: boolean
  podeEscolher: boolean
  aoVer: () => void
  aoEscolher: () => void
}) {
  const { opcao, coluna } = props
  return (
    <Paper
      onClick={props.aoVer}
      sx={{
        p: 2, minWidth: 220, flex: '1 1 220px', cursor: 'pointer',
        borderColor: opcao.selecionada ? 'success.main' : props.ativo ? 'primary.main' : undefined,
        borderWidth: opcao.selecionada || props.ativo ? 2 : 1,
      }}
    >
      <Stack spacing={1}>
        <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>{opcao.nome}</Typography>
          {opcao.selecionada && <Chip size="small" color="success" icon={<CheckIcon />} label="Escolhida" />}
        </Stack>
        <Typography variant="h5">{dinheiro(opcao.valorTotal)}</Typography>
        {coluna && (
          <Typography variant="body2" color={coluna.maisBarata ? 'success.main' : 'text.secondary'}>
            {coluna.maisBarata
              ? 'A mais barata'
              : `+ ${dinheiro(coluna.diferencaParaMaisBarata)} (${coluna.diferencaPercentual.toLocaleString('pt-BR')}%)`}
          </Typography>
        )}
        {opcao.itensSemPreco > 0 && (
          <Chip size="small" color="warning" label={`${opcao.itensSemPreco} item(ns) sem preço: total incompleto`} />
        )}
        {props.podeEscolher && (
          <Button size="small" variant="outlined" onClick={(e) => { e.stopPropagation(); props.aoEscolher() }}>
            Cliente escolheu esta
          </Button>
        )}
      </Stack>
    </Paper>
  )
}

function TabelaItens({ orcamento, opcao, editavel, aoAtualizar, aoErrar }: {
  orcamento: Orcamento
  opcao: OpcaoOrcamento
  editavel: boolean
  aoAtualizar: (o: Orcamento) => void
  aoErrar: (erro: unknown) => void
}) {
  return (
    <TableContainer>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Produto</TableCell>
            <TableCell>Marca</TableCell>
            <TableCell align="right">Qtd</TableCell>
            <TableCell>Un</TableCell>
            <TableCell align="right">Preço unit.</TableCell>
            <TableCell align="right">Total</TableCell>
            <TableCell>Estoque</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {opcao.itens.map((item) => (
            <LinhaItem key={item.id} orcamento={orcamento} item={item} editavel={editavel} aoAtualizar={aoAtualizar} aoErrar={aoErrar} />
          ))}
          <TableRow>
            <TableCell colSpan={5} align="right" sx={{ fontWeight: 600 }}>Total</TableCell>
            <TableCell align="right" sx={{ fontWeight: 600 }}>{dinheiro(opcao.valorTotal)}</TableCell>
            <TableCell />
          </TableRow>
        </TableBody>
      </Table>
    </TableContainer>
  )
}

function LinhaItem({ orcamento, item, editavel, aoAtualizar, aoErrar }: {
  orcamento: Orcamento
  item: ItemOrcamento
  editavel: boolean
  aoAtualizar: (o: Orcamento) => void
  aoErrar: (erro: unknown) => void
}) {
  const [preco, setPreco] = useState('')
  const informar = useMutation({
    // O mesmo item da lista (mesma linha e mesmo produto) aparece "sem preço" em cada opção:
    // o preço digitado vale para todas, para a loja não precisar digitar três vezes.
    mutationFn: async () => {
      const valor = Number(preco.replace(',', '.'))
      const mesmos = orcamento.opcoes.flatMap((o) =>
        o.itens.filter((i) => i.semPreco && i.ordem === item.ordem && i.produtoId === item.produtoId).map((i) => ({ opcaoId: o.id, itemId: i.id })),
      )
      let atualizado: Orcamento | undefined
      for (const { opcaoId, itemId } of mesmos) atualizado = await orcamentos.informarPreco(orcamento.id, opcaoId, itemId, valor)
      return atualizado!
    },
    onSuccess: (o) => { setPreco(''); aoAtualizar(o) },
    onError: aoErrar,
  })

  return (
    <TableRow sx={item.semPreco ? { bgcolor: '#FFF8E1' } : undefined}>
      <TableCell>
        {item.descricao}
        {item.observacao && (
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>{item.observacao}</Typography>
        )}
      </TableCell>
      <TableCell>{item.marca ?? '—'}</TableCell>
      <TableCell align="right">{quantidade(item.quantidade)}</TableCell>
      <TableCell>{item.unidadeMedida}</TableCell>
      <TableCell align="right">
        {item.precoUnitario != null ? (
          dinheiro(item.precoUnitario)
        ) : editavel ? (
          <Stack direction="row" spacing={0.5} sx={{ justifyContent: 'flex-end' }}>
            <TextField size="small" placeholder="Preço" value={preco} sx={{ width: 100 }}
              onChange={(e) => setPreco(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter' && preco && !informar.isPending) informar.mutate() }}
              error={informar.isError} helperText={informar.isError ? mensagemDeErro(informar.error) : undefined}
              slotProps={{ htmlInput: { inputMode: 'decimal' } }} />
            <Tooltip title="Salvar preço">
              <span>
                <IconButton color="primary" size="small" disabled={!preco || informar.isPending} onClick={() => informar.mutate()}>
                  <CheckIcon />
                </IconButton>
              </span>
            </Tooltip>
          </Stack>
        ) : (
          <Chip size="small" color="warning" label="sem preço" />
        )}
      </TableCell>
      <TableCell align="right">{item.precoTotal != null ? dinheiro(item.precoTotal) : '—'}</TableCell>
      <TableCell>
        {item.disponibilidade && (
          <Tooltip title={item.saldoEmEstoque != null ? `Saldo: ${quantidade(item.saldoEmEstoque)}` : 'A loja não controla o estoque deste produto'}>
            <Chip size="small" variant="outlined" label={disponibilidade[item.disponibilidade].texto} color={disponibilidade[item.disponibilidade].cor} />
          </Tooltip>
        )}
      </TableCell>
    </TableRow>
  )
}

function DialogoWhatsApp({ orcamentoId, aberto, aoFechar }: { orcamentoId: string; aberto: boolean; aoFechar: () => void }) {
  const [copiado, setCopiado] = useState(false)
  const consulta = useQuery({
    queryKey: ['whatsapp', orcamentoId],
    queryFn: () => orcamentos.whatsapp(orcamentoId),
    enabled: aberto,
    staleTime: 0,
    gcTime: 0, // sempre gera o texto de novo (o orçamento pode ter mudado)
  })

  async function copiar() {
    if (!consulta.data) return
    await navigator.clipboard.writeText(consulta.data.texto)
    setCopiado(true)
    setTimeout(() => setCopiado(false), 2000)
  }

  return (
    <Dialog open={aberto} onClose={aoFechar} fullWidth maxWidth="sm">
      <DialogTitle>Mensagem para o WhatsApp</DialogTitle>
      <DialogContent>
        <EstadoDaConsulta carregando={consulta.isFetching} erro={consulta.error} />
        {consulta.data && !consulta.isFetching && (
          <Stack spacing={1}>
            <TextField multiline fullWidth minRows={10} value={consulta.data.texto} slotProps={{ htmlInput: { readOnly: true } }} />
            {!consulta.data.telefoneCliente && (
              <Alert severity="info">
                O orçamento não tem cliente com telefone: o WhatsApp vai pedir para escolher o contato.
              </Alert>
            )}
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={aoFechar}>Fechar</Button>
        <Button onClick={() => void copiar()} disabled={!consulta.data}>{copiado ? 'Copiado!' : 'Copiar texto'}</Button>
        <Button variant="contained" color="success" startIcon={<WhatsAppIcon />} disabled={!consulta.data}
          href={consulta.data?.link ?? ''} target="_blank" rel="noopener noreferrer">
          Abrir no WhatsApp
        </Button>
      </DialogActions>
    </Dialog>
  )
}
