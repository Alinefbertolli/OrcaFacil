import BlockIcon from '@mui/icons-material/Block'
import RestoreIcon from '@mui/icons-material/Restore'
import { Box, Chip, MenuItem, TextField } from '@mui/material'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { clientes } from '../../api/endpoints'
import type { Cliente, TipoPessoa } from '../../api/tipos'
import { usePermissoes } from '../../auth/usePermissoes'
import { useAvisos } from '../../componentes/avisos/useAvisos'
import { BotaoComConfirmacao } from '../../componentes/cadastro/BotaoComConfirmacao'
import { CamposEndereco } from '../../componentes/cadastro/CamposEndereco'
import { DialogoFormulario } from '../../componentes/cadastro/DialogoFormulario'
import { TabelaCadastro } from '../../componentes/cadastro/TabelaCadastro'
import { enderecoParaForm, enderecoVazio, formParaEndereco, textoOuNulo, type EnderecoForm } from '../../util/formulario'

export function Clientes() {
  const { ehGerente } = usePermissoes()
  const avisos = useAvisos()
  const clienteQuery = useQueryClient()
  const [edicao, setEdicao] = useState<Cliente | 'novo' | null>(null)
  const [nome, setNome] = useState('')
  const [tipo, setTipo] = useState<TipoPessoa>('Fisica')
  const [documento, setDocumento] = useState('')
  const [telefone, setTelefone] = useState('')
  const [email, setEmail] = useState('')
  const [observacoes, setObservacoes] = useState('')
  const [endereco, setEndereco] = useState<EnderecoForm>(enderecoVazio)

  function abrir(c: Cliente | 'novo') {
    const novo = c === 'novo'
    setEdicao(c)
    setNome(novo ? '' : c.nome)
    setTipo(novo ? 'Fisica' : c.tipoPessoa)
    setDocumento(novo ? '' : (c.documento ?? ''))
    setTelefone(novo ? '' : (c.telefone ?? ''))
    setEmail(novo ? '' : (c.email ?? ''))
    setObservacoes(novo ? '' : (c.observacoes ?? ''))
    setEndereco(novo ? enderecoVazio : enderecoParaForm(c.endereco))
    salvar.reset()
  }

  const salvar = useMutation({
    mutationFn: () => {
      const r = {
        nome, tipoPessoa: tipo, documento: textoOuNulo(documento), telefone: textoOuNulo(telefone), email: textoOuNulo(email),
        endereco: formParaEndereco(endereco), observacoes: textoOuNulo(observacoes),
      }
      return edicao === 'novo' || edicao === null ? clientes.criar(r) : clientes.atualizar(edicao.id, r)
    },
    onSuccess: () => {
      void clienteQuery.invalidateQueries({ queryKey: ['clientes'] })
      setEdicao(null)
      avisos.sucesso('Cliente salvo.')
    },
  })
  const desativar = useMutation({
    mutationFn: (c: Cliente) => (c.ativo ? clientes.desativar(c.id) : clientes.reativar(c.id).then(() => undefined)),
    onSuccess: (_, c) => {
      void clienteQuery.invalidateQueries({ queryKey: ['clientes'] })
      avisos.sucesso(c.ativo ? 'Cliente desativado.' : 'Cliente reativado.')
    },
    onError: avisos.erro,
  })

  return (
    <>
      <TabelaCadastro<Cliente>
        titulo="Clientes"
        descricao="Com o telefone cadastrado, o botão do WhatsApp abre a conversa direto com o cliente."
        chave="clientes"
        buscar={(f) => clientes.listar(f)}
        idDe={(c) => c.id}
        aoNovo={() => abrir('novo')}
        textoNovo="Novo cliente"
        aoClicar={abrir}
        comFiltroInativos
        colunas={[
          {
            titulo: 'Nome',
            valor: (c) => (
              <Box sx={{ opacity: c.ativo ? 1 : 0.5 }}>
                {c.nome} {!c.ativo && <Chip size="small" label="inativo" sx={{ ml: 1 }} />}
              </Box>
            ),
          },
          { titulo: 'Tipo', valor: (c) => (c.tipoPessoa === 'Fisica' ? 'Pessoa física' : 'Empresa') },
          { titulo: 'Telefone', valor: (c) => c.telefone ?? '—' },
          { titulo: 'E-mail', valor: (c) => c.email ?? '—' },
        ]}
        acoes={(c) =>
          ehGerente && (
            <BotaoComConfirmacao icone={c.ativo ? <BlockIcon fontSize="small" /> : <RestoreIcon fontSize="small" />}
              dica={c.ativo ? 'Desativar' : 'Reativar'} perigoso={c.ativo} textoConfirmar={c.ativo ? 'Desativar' : 'Reativar'}
              pergunta={c.ativo ? `Desativar o cliente "${c.nome}"? Os orçamentos dele são mantidos.` : `Reativar o cliente "${c.nome}"?`}
              aoConfirmar={() => desativar.mutate(c)} />
          )
        }
      />

      <DialogoFormulario aberto={edicao !== null} titulo={edicao === 'novo' ? 'Novo cliente' : 'Editar cliente'} largura="md"
        aoFechar={() => setEdicao(null)} aoSalvar={() => salvar.mutate()} salvando={salvar.isPending} erro={salvar.error}>
        <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '3fr 1fr' } }}>
          <TextField label="Nome" required autoFocus value={nome} onChange={(e) => setNome(e.target.value)} />
          <TextField select label="Tipo" value={tipo} onChange={(e) => setTipo(e.target.value as TipoPessoa)}>
            <MenuItem value="Fisica">Pessoa física</MenuItem>
            <MenuItem value="Juridica">Empresa</MenuItem>
          </TextField>
        </Box>
        <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr 1fr' } }}>
          <TextField label={tipo === 'Fisica' ? 'CPF (opcional)' : 'CNPJ (opcional)'} value={documento} onChange={(e) => setDocumento(e.target.value)} />
          <TextField label="Telefone / WhatsApp" value={telefone} onChange={(e) => setTelefone(e.target.value)} placeholder="(11) 98765-4321" />
          <TextField label="E-mail" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
        </Box>
        <CamposEndereco valor={endereco} aoMudar={setEndereco} />
        <TextField label="Observações (opcional)" multiline minRows={2} value={observacoes} onChange={(e) => setObservacoes(e.target.value)} />
      </DialogoFormulario>
    </>
  )
}
