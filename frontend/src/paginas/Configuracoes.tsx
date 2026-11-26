import { Alert, Box, Button, Paper, Stack, TextField, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { mensagemDeErro } from '../api/cliente'
import { empresa } from '../api/endpoints'
import type { ConfiguracoesEmpresa, ModoOrcamento } from '../api/tipos'
import { usePermissoes } from '../auth/usePermissoes'
import { useAvisos } from '../componentes/avisos/useAvisos'
import { CamposEndereco } from '../componentes/cadastro/CamposEndereco'
import { EstadoDaConsulta } from '../componentes/Carregando'
import { enderecoParaForm, formParaEndereco, textoOuNulo, type EnderecoForm } from '../util/formulario'

export function Configuracoes() {
  const consulta = useQuery({ queryKey: ['empresa', 'configuracoes'], queryFn: empresa.configuracoes })

  // Só mostramos o formulário depois que os dados chegam da API. Assim o formulário
  // já nasce preenchido (valor inicial do useState), sem precisar de useEffect.
  if (!consulta.data) return <EstadoDaConsulta carregando={consulta.isPending} erro={consulta.error} />
  return <FormularioConfiguracoes dados={consulta.data} />
}

function FormularioConfiguracoes({ dados }: { dados: ConfiguracoesEmpresa }) {
  const { ehAdministrador } = usePermissoes()
  const avisos = useAvisos()
  const clienteQuery = useQueryClient()

  const [modo, setModo] = useState<ModoOrcamento>(dados.modoOrcamentoPadrao)
  const [telefone, setTelefone] = useState(dados.telefone ?? '')
  const [email, setEmail] = useState(dados.email ?? '')
  const [endereco, setEndereco] = useState<EnderecoForm>(() => enderecoParaForm(dados.endereco))

  const salvar = useMutation({
    mutationFn: () =>
      empresa.salvar({ modoOrcamentoPadrao: modo, telefone: textoOuNulo(telefone), email: textoOuNulo(email), endereco: formParaEndereco(endereco) }),
    onSuccess: (dados) => {
      clienteQuery.setQueryData(['empresa', 'configuracoes'], dados)
      avisos.sucesso('Configurações salvas.')
    },
  })

  function enviar(evento: FormEvent) {
    evento.preventDefault()
    if (ehAdministrador && !salvar.isPending) salvar.mutate()
  }

  return (
    <Stack component="form" spacing={2} sx={{ maxWidth: 820 }} onSubmit={enviar} noValidate>
      <Box>
        <Typography variant="h5">Configurações da loja</Typography>
        <Typography color="text.secondary">{dados.razaoSocial} · CNPJ {dados.cnpj}</Typography>
      </Box>
      {!ehAdministrador && <Alert severity="info">Só o administrador da loja pode alterar estas configurações.</Alert>}

      <Paper sx={{ p: 2 }}>
        <Stack spacing={1}>
          <Typography variant="h6">Orçamento</Typography>
          <Typography variant="body2" color="text.secondary">
            Modo usado quando a pessoa escolhe &quot;Padrão da loja&quot; ao gerar o orçamento.
          </Typography>
          <ToggleButtonGroup exclusive value={modo} disabled={!ehAdministrador}
            onChange={(_, novo: ModoOrcamento | null) => novo && setModo(novo)}>
            <ToggleButton value="Comparativo">Comparativo (até 3 opções)</ToggleButton>
            <ToggleButton value="Unico">Único (itens exatos)</ToggleButton>
          </ToggleButtonGroup>
        </Stack>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Stack spacing={2}>
          <Box>
            <Typography variant="h6">Contato da loja</Typography>
            <Typography variant="body2" color="text.secondary">Aparece no cabeçalho do PDF e no fim da mensagem do WhatsApp.</Typography>
          </Box>
          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' } }}>
            <TextField label="Telefone / WhatsApp" value={telefone} disabled={!ehAdministrador} onChange={(e) => setTelefone(e.target.value)} />
            <TextField label="E-mail" type="email" value={email} disabled={!ehAdministrador} onChange={(e) => setEmail(e.target.value)} />
          </Box>
          <CamposEndereco valor={endereco} aoMudar={setEndereco} desabilitado={!ehAdministrador} />
        </Stack>
      </Paper>

      {salvar.isError && <Alert severity="error">{mensagemDeErro(salvar.error)}</Alert>}
      {ehAdministrador && (
        <Box>
          <Button type="submit" variant="contained" size="large" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando...' : 'Salvar configurações'}
          </Button>
        </Box>
      )}
    </Stack>
  )
}
