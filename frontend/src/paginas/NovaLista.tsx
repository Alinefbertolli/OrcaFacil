import UploadFileIcon from '@mui/icons-material/UploadFile'
import {
  Alert,
  Box,
  Button,
  Paper,
  Stack,
  Tab,
  Tabs,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material'
import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { mensagemDeErro } from '../api/cliente'
import { importacoes } from '../api/endpoints'
import type { ClienteResumo } from '../api/tipos'
import { SeletorCliente } from '../componentes/Seletores'

const EXEMPLO = `[10:32, 25/11/2026] Maria: Bom dia! Segue a lista:
[10:32, 25/11/2026] Maria: - 2 cadernos 10 matérias
[10:33, 25/11/2026] Maria: - 3 borrachas brancas`

/** Passo 1 do fluxo: a loja cola a mensagem do cliente ou envia o arquivo (PDF, foto, Excel, Word). */
export function NovaLista() {
  const navegar = useNavigate()
  const [aba, setAba] = useState<'texto' | 'arquivo'>('texto')
  const [texto, setTexto] = useState('')
  const [formato, setFormato] = useState<'Texto' | 'WhatsApp'>('WhatsApp')
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [cliente, setCliente] = useState<ClienteResumo | null>(null)

  const importar = useMutation({
    mutationFn: () =>
      aba === 'texto'
        ? importacoes.importarTexto(texto, formato, cliente?.id ?? null)
        : importacoes.importarArquivo(arquivo!, cliente?.id ?? null),
    onSuccess: (lista) => navegar(`/listas/${lista.id}`),
  })

  const podeEnviar = aba === 'texto' ? texto.trim().length > 0 : arquivo !== null

  return (
    <Stack spacing={2} sx={{ maxWidth: 820 }}>
      <Box>
        <Typography variant="h5">Nova lista do cliente</Typography>
        <Typography color="text.secondary">
          Cole a mensagem do WhatsApp ou envie o arquivo. Na próxima tela você confere os itens.
        </Typography>
      </Box>

      <Paper sx={{ p: 2 }}>
        <Tabs value={aba} onChange={(_, nova: 'texto' | 'arquivo') => setAba(nova)} sx={{ mb: 2 }}>
          <Tab value="texto" label="Colar texto" />
          <Tab value="arquivo" label="Enviar arquivo" />
        </Tabs>

        {aba === 'texto' ? (
          <Stack spacing={2}>
            <ToggleButtonGroup
              exclusive
              size="small"
              value={formato}
              onChange={(_, novo: 'Texto' | 'WhatsApp' | null) => novo && setFormato(novo)}
            >
              <ToggleButton value="WhatsApp">Conversa do WhatsApp</ToggleButton>
              <ToggleButton value="Texto">Texto simples</ToggleButton>
            </ToggleButtonGroup>
            <TextField
              multiline
              minRows={8}
              placeholder={EXEMPLO}
              value={texto}
              onChange={(e) => setTexto(e.target.value)}
            />
          </Stack>
        ) : (
          <Stack spacing={1} sx={{ alignItems: 'flex-start' }}>
            <Button component="label" variant="outlined" startIcon={<UploadFileIcon />}>
              Escolher arquivo
              <input
                hidden
                type="file"
                accept=".pdf,.png,.jpg,.jpeg,.xlsx,.docx,.txt"
                onChange={(e) => setArquivo(e.target.files?.[0] ?? null)}
              />
            </Button>
            <Typography variant="body2" color="text.secondary">
              {arquivo ? `${arquivo.name} (${Math.ceil(arquivo.size / 1024)} KB)` : 'PDF, foto da lista, Excel, Word ou texto (até 10 MB)'}
            </Typography>
          </Stack>
        )}
      </Paper>

      <Paper sx={{ p: 2 }}>
        <SeletorCliente valor={cliente} aoMudar={setCliente} />
      </Paper>

      {importar.isError && <Alert severity="error">{mensagemDeErro(importar.error)}</Alert>}

      <Box>
        <Button variant="contained" size="large" disabled={!podeEnviar || importar.isPending} onClick={() => importar.mutate()}>
          {importar.isPending ? 'Lendo a lista...' : 'Ler a lista'}
        </Button>
      </Box>
    </Stack>
  )
}
