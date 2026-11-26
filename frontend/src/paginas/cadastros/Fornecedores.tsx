import BlockIcon from '@mui/icons-material/Block'
import RestoreIcon from '@mui/icons-material/Restore'
import { Box, Chip, Rating, TextField, Typography } from '@mui/material'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { fornecedores } from '../../api/endpoints'
import type { Fornecedor } from '../../api/tipos'
import { usePermissoes } from '../../auth/usePermissoes'
import { useAvisos } from '../../componentes/avisos/useAvisos'
import { BotaoComConfirmacao } from '../../componentes/cadastro/BotaoComConfirmacao'
import { CamposEndereco } from '../../componentes/cadastro/CamposEndereco'
import { DialogoFormulario } from '../../componentes/cadastro/DialogoFormulario'
import { TabelaCadastro } from '../../componentes/cadastro/TabelaCadastro'
import { enderecoParaForm, enderecoVazio, formParaEndereco, textoOuNulo, type EnderecoForm } from '../../util/formulario'

export function Fornecedores() {
  const { ehGerente } = usePermissoes()
  const avisos = useAvisos()
  const clienteQuery = useQueryClient()
  const [edicao, setEdicao] = useState<Fornecedor | 'novo' | null>(null)
  const [razaoSocial, setRazaoSocial] = useState('')
  const [nomeFantasia, setNomeFantasia] = useState('')
  const [cnpj, setCnpj] = useState('')
  const [contato, setContato] = useState('')
  const [telefone, setTelefone] = useState('')
  const [email, setEmail] = useState('')
  const [avaliacao, setAvaliacao] = useState<number | null>(null)
  const [endereco, setEndereco] = useState<EnderecoForm>(enderecoVazio)

  function abrir(f: Fornecedor | 'novo') {
    const novo = f === 'novo'
    setEdicao(f)
    setRazaoSocial(novo ? '' : f.razaoSocial)
    setNomeFantasia(novo ? '' : (f.nomeFantasia ?? ''))
    setCnpj(novo ? '' : f.cnpj)
    setContato(novo ? '' : (f.contato ?? ''))
    setTelefone(novo ? '' : (f.telefone ?? ''))
    setEmail(novo ? '' : (f.email ?? ''))
    setAvaliacao(novo ? null : f.avaliacao)
    setEndereco(novo ? enderecoVazio : enderecoParaForm(f.endereco))
    salvar.reset()
  }

  const salvar = useMutation({
    mutationFn: () => {
      const r = {
        razaoSocial, nomeFantasia: textoOuNulo(nomeFantasia), cnpj, contato: textoOuNulo(contato), telefone: textoOuNulo(telefone),
        email: textoOuNulo(email), endereco: formParaEndereco(endereco), avaliacao,
      }
      return edicao === 'novo' || edicao === null ? fornecedores.criar(r) : fornecedores.atualizar(edicao.id, r)
    },
    onSuccess: () => {
      void clienteQuery.invalidateQueries({ queryKey: ['fornecedores'] })
      setEdicao(null)
      avisos.sucesso('Fornecedor salvo.')
    },
  })
  const desativar = useMutation({
    mutationFn: (f: Fornecedor) => (f.ativo ? fornecedores.desativar(f.id) : fornecedores.reativar(f.id).then(() => undefined)),
    onSuccess: (_, f) => {
      void clienteQuery.invalidateQueries({ queryKey: ['fornecedores'] })
      avisos.sucesso(f.ativo ? 'Fornecedor desativado.' : 'Fornecedor reativado.')
    },
    onError: avisos.erro,
  })

  return (
    <>
      <TabelaCadastro<Fornecedor>
        titulo="Fornecedores"
        descricao="De quem a loja compra. (Os preços de custo por fornecedor ficam para uma próxima etapa.)"
        chave="fornecedores"
        buscar={(f) => fornecedores.listar(f)}
        idDe={(f) => f.id}
        aoNovo={() => abrir('novo')}
        textoNovo="Novo fornecedor"
        aoClicar={abrir}
        comFiltroInativos
        colunas={[
          {
            titulo: 'Fornecedor',
            valor: (f) => (
              <Box sx={{ opacity: f.ativo ? 1 : 0.5 }}>
                {f.nomeFantasia ?? f.razaoSocial} {!f.ativo && <Chip size="small" label="inativo" sx={{ ml: 1 }} />}
              </Box>
            ),
          },
          { titulo: 'CNPJ', valor: (f) => f.cnpj },
          { titulo: 'Contato', valor: (f) => [f.contato, f.telefone].filter(Boolean).join(' · ') || '—' },
          { titulo: 'Avaliação', valor: (f) => (f.avaliacao === null ? '—' : <Rating value={f.avaliacao} precision={0.5} readOnly size="small" />) },
        ]}
        acoes={(f) =>
          ehGerente && (
            <BotaoComConfirmacao icone={f.ativo ? <BlockIcon fontSize="small" /> : <RestoreIcon fontSize="small" />}
              dica={f.ativo ? 'Desativar' : 'Reativar'} perigoso={f.ativo} textoConfirmar={f.ativo ? 'Desativar' : 'Reativar'}
              pergunta={f.ativo ? `Desativar o fornecedor "${f.razaoSocial}"?` : `Reativar o fornecedor "${f.razaoSocial}"?`}
              aoConfirmar={() => desativar.mutate(f)} />
          )
        }
      />

      <DialogoFormulario aberto={edicao !== null} titulo={edicao === 'novo' ? 'Novo fornecedor' : 'Editar fornecedor'} largura="md"
        aoFechar={() => setEdicao(null)} aoSalvar={() => salvar.mutate()} salvando={salvar.isPending} erro={salvar.error}>
        <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' } }}>
          <TextField label="Razão social" required autoFocus value={razaoSocial} onChange={(e) => setRazaoSocial(e.target.value)} />
          <TextField label="Nome fantasia (opcional)" value={nomeFantasia} onChange={(e) => setNomeFantasia(e.target.value)} />
        </Box>
        <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' } }}>
          <TextField label="CNPJ" required value={cnpj} onChange={(e) => setCnpj(e.target.value)} placeholder="00.000.000/0000-00" />
          <TextField label="Pessoa de contato" value={contato} onChange={(e) => setContato(e.target.value)} />
        </Box>
        <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' } }}>
          <TextField label="Telefone" value={telefone} onChange={(e) => setTelefone(e.target.value)} />
          <TextField label="E-mail" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
        </Box>
        <Box>
          <Typography variant="body2" sx={{ mb: 0.5 }}>Avaliação da equipe de compras (opcional)</Typography>
          <Rating value={avaliacao} precision={0.5} onChange={(_, novo) => setAvaliacao(novo)} />
        </Box>
        <CamposEndereco valor={endereco} aoMudar={setEndereco} />
      </DialogoFormulario>
    </>
  )
}
