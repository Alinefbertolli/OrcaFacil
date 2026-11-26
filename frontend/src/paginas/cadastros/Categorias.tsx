import DeleteIcon from '@mui/icons-material/DeleteOutlineOutlined'
import { MenuItem, TextField } from '@mui/material'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { categorias } from '../../api/endpoints'
import type { Categoria } from '../../api/tipos'
import { usePermissoes } from '../../auth/usePermissoes'
import { useAvisos } from '../../componentes/avisos/useAvisos'
import { BotaoComConfirmacao } from '../../componentes/cadastro/BotaoComConfirmacao'
import { DialogoFormulario } from '../../componentes/cadastro/DialogoFormulario'
import { TabelaCadastro } from '../../componentes/cadastro/TabelaCadastro'
import { textoOuNulo } from '../../util/formulario'

export function Categorias() {
  const { ehGerente } = usePermissoes()
  const avisos = useAvisos()
  const clienteQuery = useQueryClient()
  const [edicao, setEdicao] = useState<Categoria | 'nova' | null>(null)
  const [nome, setNome] = useState('')
  const [descricao, setDescricao] = useState('')
  const [paiId, setPaiId] = useState('')

  // Lista completa para escolher a categoria "pai" (ex.: Escolar > Cadernos).
  const todas = useQuery({ queryKey: ['categorias', 'todas'], queryFn: categorias.todas, enabled: edicao !== null })

  function abrir(c: Categoria | 'nova') {
    setEdicao(c)
    setNome(c === 'nova' ? '' : c.nome)
    setDescricao(c === 'nova' ? '' : (c.descricao ?? ''))
    setPaiId(c === 'nova' ? '' : (c.categoriaPaiId ?? ''))
    salvar.reset()
  }

  const salvar = useMutation({
    mutationFn: () => {
      const r = { nome, descricao: textoOuNulo(descricao), categoriaPaiId: paiId || null }
      return edicao === 'nova' || edicao === null ? categorias.criar(r) : categorias.atualizar(edicao.id, r)
    },
    onSuccess: () => {
      void clienteQuery.invalidateQueries({ queryKey: ['categorias'] })
      setEdicao(null)
      avisos.sucesso('Categoria salva.')
    },
  })
  const excluir = useMutation({
    mutationFn: (id: string) => categorias.excluir(id),
    onSuccess: () => { void clienteQuery.invalidateQueries({ queryKey: ['categorias'] }); avisos.sucesso('Categoria excluída.') },
    onError: avisos.erro,
  })

  const idAtual = edicao !== null && edicao !== 'nova' ? edicao.id : null

  return (
    <>
      <TabelaCadastro<Categoria>
        titulo="Categorias"
        descricao="Organizam o catálogo. Sem grupo de equivalência, o orçamento comparativo procura substitutos na mesma categoria."
        chave="categorias"
        buscar={(f) => categorias.listar(f)}
        idDe={(c) => c.id}
        aoNovo={() => abrir('nova')}
        textoNovo="Nova categoria"
        aoClicar={abrir}
        colunas={[
          { titulo: 'Nome', valor: (c) => c.nome },
          { titulo: 'Dentro de', valor: (c) => c.categoriaPaiNome ?? '—' },
          { titulo: 'Descrição', valor: (c) => c.descricao ?? '' },
        ]}
        acoes={(c) =>
          ehGerente && (
            <BotaoComConfirmacao icone={<DeleteIcon fontSize="small" />} dica="Excluir" perigoso textoConfirmar="Excluir"
              pergunta={`Excluir a categoria "${c.nome}"? Só é possível se ela não tiver produtos nem subcategorias.`}
              aoConfirmar={() => excluir.mutate(c.id)} />
          )
        }
      />

      <DialogoFormulario aberto={edicao !== null} titulo={edicao === 'nova' ? 'Nova categoria' : 'Editar categoria'}
        aoFechar={() => setEdicao(null)} aoSalvar={() => salvar.mutate()} salvando={salvar.isPending} erro={salvar.error}>
        <TextField label="Nome" required autoFocus value={nome} onChange={(e) => setNome(e.target.value)} />
        <TextField select label="Dentro de (opcional)" value={paiId} onChange={(e) => setPaiId(e.target.value)}>
          <MenuItem value="">— Nenhuma (categoria principal)</MenuItem>
          {(todas.data?.itens ?? []).filter((c) => c.id !== idAtual).map((c) => (
            <MenuItem key={c.id} value={c.id}>{c.nome}</MenuItem>
          ))}
        </TextField>
        <TextField label="Descrição (opcional)" multiline minRows={2} value={descricao} onChange={(e) => setDescricao(e.target.value)} />
      </DialogoFormulario>
    </>
  )
}
