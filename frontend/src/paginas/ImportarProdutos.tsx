import DownloadIcon from '@mui/icons-material/Download'
import UploadFileIcon from '@mui/icons-material/UploadFile'
import {
  Alert,
  Box,
  Button,
  Chip,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type ChangeEvent } from 'react'
import { mensagemDeErro } from '../api/cliente'
import { produtos } from '../api/endpoints'
import type { LinhaImportacaoCatalogo, ResultadoImportacaoCatalogo, SituacaoLinhaImportacao } from '../api/tipos'
import { usePermissoes } from '../auth/usePermissoes'
import { useAvisos } from '../componentes/avisos/useAvisos'
import { baixarArquivo } from '../util/baixarArquivo'

type Filtro = 'mudancas' | 'erros' | 'todas'

const situacoes: Record<SituacaoLinhaImportacao, { texto: string; cor: 'success' | 'info' | 'default' | 'error' }> = {
  Criado: { texto: 'Novo', cor: 'success' },
  Atualizado: { texto: 'Atualizado', cor: 'info' },
  SemAlteracao: { texto: 'Sem alteração', cor: 'default' },
  Erro: { texto: 'Erro', cor: 'error' },
}

/** Mostramos no máximo estas linhas na tabela (planilhas podem ter milhares). */
const LIMITE_TABELA = 300

/**
 * Importação de produtos, preços e estoque por planilha (.xlsx ou .csv), por exemplo exportada do ERP.
 * Dois passos, para ninguém estragar o catálogo sem querer:
 * 1) "Analisar": a API simula e devolve o que VAI acontecer, linha por linha (nada é gravado);
 * 2) "Importar": envia o mesmo arquivo de novo, agora para gravar.
 */
export function ImportarProdutos() {
  const { ehGerente } = usePermissoes()
  const avisos = useAvisos()
  const clienteQuery = useQueryClient()
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [resultado, setResultado] = useState<ResultadoImportacaoCatalogo | null>(null)
  const [filtro, setFiltro] = useState<Filtro>('mudancas')

  const modelo = useMutation({
    mutationFn: produtos.baixarModelo,
    onSuccess: ({ conteudo, nome }) => baixarArquivo(conteudo, nome),
    onError: (e) => avisos.erro(mensagemDeErro(e)),
  })

  const enviar = useMutation({
    mutationFn: (simular: boolean) => {
      if (!arquivo) throw new Error('Escolha o arquivo da planilha.')
      return produtos.importar(arquivo, simular)
    },
    onSuccess: (dados) => {
      setResultado(dados)
      if (!dados.simulacao) {
        avisos.sucesso('Importação concluída.')
        // Os dados mudaram: as outras telas buscam de novo quando forem abertas.
        for (const chave of ['produtos', 'estoque', 'categorias', 'marcas'])
          void clienteQuery.invalidateQueries({ queryKey: [chave] })
      }
    },
  })

  function escolherArquivo(evento: ChangeEvent<HTMLInputElement>) {
    setArquivo(evento.target.files?.[0] ?? null)
    setResultado(null) // arquivo novo: a prévia antiga não vale mais
    enviar.reset()
    evento.target.value = '' // permite escolher o mesmo arquivo de novo depois de editá-lo
  }

  const vaiMudar = resultado ? resultado.criados + resultado.atualizados : 0
  const linhas = resultado ? filtrar(resultado.linhas, filtro) : []

  return (
    <Stack spacing={2} sx={{ maxWidth: 1100 }}>
      <Box>
        <Typography variant="h5">Importar produtos de planilha</Typography>
        <Typography color="text.secondary">
          Traga produtos, preços de venda e estoque do seu ERP ou de uma planilha (.xlsx ou .csv).
        </Typography>
      </Box>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" gutterBottom>1. Prepare a planilha</Typography>
        <Box component="ul" sx={{ mt: 0, pl: 3, '& li': { mb: 0.5 } }}>
          <li>A primeira linha precisa ter os títulos. Obrigatórios: <b>Código</b> e <b>Nome</b> (ou Descrição).</li>
          <li>Opcionais: Preço de venda, Estoque, Estoque mínimo, Unidade, Categoria, Marca, Grupo de equivalência.</li>
          <li>O produto é encontrado pelo <b>código</b>. Código novo = produto novo.</li>
          <li><b>Célula vazia não muda nada.</b> Produtos que não estão na planilha continuam como estão.</li>
          <li>A coluna Estoque vira o saldo atual (registrado como inventário, com histórico).</li>
        </Box>
        <Button startIcon={<DownloadIcon />} onClick={() => modelo.mutate()} disabled={modelo.isPending}>
          Baixar planilha modelo
        </Button>
      </Paper>

      {!ehGerente ? (
        <Alert severity="info">Só gerente ou administrador pode importar planilhas.</Alert>
      ) : (
        <Paper sx={{ p: 2 }}>
          <Typography variant="h6" gutterBottom>2. Envie e confira</Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ alignItems: { sm: 'center' } }}>
            <Button component="label" variant="outlined" startIcon={<UploadFileIcon />}>
              Escolher arquivo
              <input hidden type="file" accept=".xlsx,.csv" onChange={escolherArquivo} />
            </Button>
            <Typography color={arquivo ? 'text.primary' : 'text.secondary'} sx={{ flexGrow: 1 }}>
              {arquivo?.name ?? 'Nenhum arquivo escolhido'}
            </Typography>
            <Button variant="contained" disabled={!arquivo || enviar.isPending} onClick={() => enviar.mutate(true)}>
              {enviar.isPending && enviar.variables ? 'Analisando...' : 'Analisar planilha'}
            </Button>
          </Stack>
          {enviar.isError && <Alert severity="error" sx={{ mt: 2 }}>{mensagemDeErro(enviar.error)}</Alert>}
        </Paper>
      )}

      {resultado && (
        <Paper sx={{ p: 2 }}>
          <Stack spacing={2}>
            <Typography variant="h6">
              {resultado.simulacao ? '3. Prévia da importação' : 'Importação concluída'}
            </Typography>

            {resultado.simulacao ? (
              <Alert severity="warning">
                Isto é só uma <b>prévia</b>: nada foi gravado ainda. Confira as linhas e clique em &quot;Importar&quot;.
              </Alert>
            ) : (
              <Alert severity="success">Os produtos foram gravados. As linhas com erro foram puladas.</Alert>
            )}

            <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
              <Chip color="success" label={`${resultado.criados} novo(s)`} />
              <Chip color="info" label={`${resultado.atualizados} atualizado(s)`} />
              <Chip label={`${resultado.semAlteracao} sem alteração`} />
              <Chip color="error" variant={resultado.comErro ? 'filled' : 'outlined'} label={`${resultado.comErro} com erro`} />
              <Chip variant="outlined" label={`${resultado.estoquesAjustados} estoque(s) ajustado(s)`} />
            </Stack>

            <Typography variant="body2" color="text.secondary">
              Colunas reconhecidas em &quot;{resultado.nomeArquivo}&quot;: {resultado.colunasReconhecidas.join(', ')}.
              {resultado.categoriasCriadas.length > 0 && <> Categorias novas: {resultado.categoriasCriadas.join(', ')}.</>}
              {resultado.marcasCriadas.length > 0 && <> Marcas novas (nível 3): {resultado.marcasCriadas.join(', ')}.</>}
            </Typography>

            <ToggleButtonGroup size="small" exclusive value={filtro} onChange={(_, novo: Filtro | null) => novo && setFiltro(novo)}>
              <ToggleButton value="mudancas">Mudanças e erros</ToggleButton>
              <ToggleButton value="erros">Só erros</ToggleButton>
              <ToggleButton value="todas">Todas as linhas</ToggleButton>
            </ToggleButtonGroup>

            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Linha</TableCell>
                  <TableCell>Código</TableCell>
                  <TableCell>Nome</TableCell>
                  <TableCell>Situação</TableCell>
                  <TableCell>O que acontece</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {linhas.slice(0, LIMITE_TABELA).map((l) => (
                  <TableRow key={l.linha}>
                    <TableCell>{l.linha}</TableCell>
                    <TableCell>{l.codigo ?? '—'}</TableCell>
                    <TableCell>{l.nome ?? '—'}</TableCell>
                    <TableCell>
                      <Chip size="small" color={situacoes[l.situacao].cor} label={situacoes[l.situacao].texto} />
                    </TableCell>
                    <TableCell>{l.detalhes.join('; ') || '—'}</TableCell>
                  </TableRow>
                ))}
                {linhas.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={5}>Nenhuma linha neste filtro.</TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
            {linhas.length > LIMITE_TABELA && (
              <Typography variant="body2" color="text.secondary">
                Mostrando as primeiras {LIMITE_TABELA} de {linhas.length} linhas.
              </Typography>
            )}

            {resultado.simulacao && (
              <Box>
                <Button variant="contained" size="large" disabled={vaiMudar === 0 || enviar.isPending}
                  onClick={() => enviar.mutate(false)}>
                  {enviar.isPending && !enviar.variables ? 'Importando...' : `Importar ${vaiMudar} produto(s)`}
                </Button>
                {vaiMudar === 0 && (
                  <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                    Nada para importar: a planilha é igual ao cadastro (ou só tem linhas com erro).
                  </Typography>
                )}
              </Box>
            )}
          </Stack>
        </Paper>
      )}
    </Stack>
  )
}

function filtrar(linhas: LinhaImportacaoCatalogo[], filtro: Filtro) {
  switch (filtro) {
    case 'erros':
      return linhas.filter((l) => l.situacao === 'Erro')
    case 'mudancas':
      return linhas.filter((l) => l.situacao !== 'SemAlteracao')
    default:
      return linhas
  }
}
