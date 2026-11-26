import DeleteIcon from '@mui/icons-material/DeleteOutlineOutlined'
import { Box, Rating, TextField, Typography } from '@mui/material'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { marcas } from '../../api/endpoints'
import type { Marca } from '../../api/tipos'
import { usePermissoes } from '../../auth/usePermissoes'
import { useAvisos } from '../../componentes/avisos/useAvisos'
import { BotaoComConfirmacao } from '../../componentes/cadastro/BotaoComConfirmacao'
import { DialogoFormulario } from '../../componentes/cadastro/DialogoFormulario'
import { TabelaCadastro } from '../../componentes/cadastro/TabelaCadastro'

const NIVEIS = ['', 'Pouco conhecida', 'Conhecida localmente', 'Conhecida', 'Muito conhecida', 'Líder de mercado']

export function Marcas() {
  const { ehGerente } = usePermissoes()
  const avisos = useAvisos()
  const clienteQuery = useQueryClient()
  const [edicao, setEdicao] = useState<Marca | 'nova' | null>(null)
  const [nome, setNome] = useState('')
  const [nivel, setNivel] = useState(3)

  function abrir(marca: Marca | 'nova') {
    setEdicao(marca)
    setNome(marca === 'nova' ? '' : marca.nome)
    setNivel(marca === 'nova' ? 3 : marca.nivelReconhecimento)
    salvar.reset()
  }

  const salvar = useMutation({
    mutationFn: () => {
      const requisicao = { nome, nivelReconhecimento: nivel }
      return edicao === 'nova' || edicao === null ? marcas.criar(requisicao) : marcas.atualizar(edicao.id, requisicao)
    },
    onSuccess: () => {
      void clienteQuery.invalidateQueries({ queryKey: ['marcas'] })
      setEdicao(null)
      avisos.sucesso('Marca salva.')
    },
  })
  const excluir = useMutation({
    mutationFn: (id: string) => marcas.excluir(id),
    onSuccess: () => { void clienteQuery.invalidateQueries({ queryKey: ['marcas'] }); avisos.sucesso('Marca excluída.') },
    onError: avisos.erro,
  })

  return (
    <>
      <TabelaCadastro<Marca>
        titulo="Marcas"
        descricao="O nível de reconhecimento define as opções Custo-benefício (nível 3+) e Marcas reconhecidas."
        chave="marcas"
        buscar={(f) => marcas.listar(f)}
        idDe={(m) => m.id}
        aoNovo={() => abrir('nova')}
        textoNovo="Nova marca"
        aoClicar={abrir}
        colunas={[
          { titulo: 'Nome', valor: (m) => m.nome },
          {
            titulo: 'Reconhecimento',
            valor: (m) => (
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Rating value={m.nivelReconhecimento} readOnly size="small" />
                <Typography variant="caption" color="text.secondary">{NIVEIS[m.nivelReconhecimento]}</Typography>
              </Box>
            ),
          },
        ]}
        acoes={(m) =>
          ehGerente && (
            <BotaoComConfirmacao icone={<DeleteIcon fontSize="small" />} dica="Excluir" perigoso textoConfirmar="Excluir"
              pergunta={`Excluir a marca "${m.nome}"? Só é possível se nenhum produto usar essa marca.`}
              aoConfirmar={() => excluir.mutate(m.id)} />
          )
        }
      />

      <DialogoFormulario aberto={edicao !== null} titulo={edicao === 'nova' ? 'Nova marca' : 'Editar marca'}
        aoFechar={() => setEdicao(null)} aoSalvar={() => salvar.mutate()} salvando={salvar.isPending} erro={salvar.error}>
        <TextField label="Nome" required autoFocus value={nome} onChange={(e) => setNome(e.target.value)} />
        <Box>
          <Typography variant="body2" sx={{ mb: 0.5 }}>Nível de reconhecimento</Typography>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Rating value={nivel} onChange={(_, novo) => novo && setNivel(novo)} />
            <Typography variant="body2" color="text.secondary">{NIVEIS[nivel]}</Typography>
          </Box>
        </Box>
      </DialogoFormulario>
    </>
  )
}
