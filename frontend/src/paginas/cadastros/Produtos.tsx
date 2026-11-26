import BlockIcon from '@mui/icons-material/Block'
import RestoreIcon from '@mui/icons-material/Restore'
import { Box, Chip, InputAdornment, MenuItem, TextField } from '@mui/material'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { categorias, marcas, produtos } from '../../api/endpoints'
import type { ProdutoResumo } from '../../api/tipos'
import { usePermissoes } from '../../auth/usePermissoes'
import { useAvisos } from '../../componentes/avisos/useAvisos'
import { BotaoComConfirmacao } from '../../componentes/cadastro/BotaoComConfirmacao'
import { DialogoFormulario } from '../../componentes/cadastro/DialogoFormulario'
import { TabelaCadastro } from '../../componentes/cadastro/TabelaCadastro'
import { dinheiro } from '../../util/formato'
import { numeroParaTexto, paraNumero, textoOuNulo } from '../../util/formulario'

interface FormProduto {
  codigo: string
  nome: string
  descricao: string
  unidadeMedida: string
  categoriaId: string
  marcaId: string
  precoVenda: string
  grupoEquivalencia: string
}

const vazio: FormProduto = {
  codigo: '', nome: '', descricao: '', unidadeMedida: 'UN', categoriaId: '', marcaId: '', precoVenda: '', grupoEquivalencia: '',
}

export function Produtos() {
  const { ehGerente } = usePermissoes()
  const avisos = useAvisos()
  const clienteQuery = useQueryClient()
  const [edicaoId, setEdicaoId] = useState<string | 'novo' | null>(null)
  const [form, setForm] = useState<FormProduto>(vazio)
  const mudar = (campo: keyof FormProduto, valor: string) => setForm((f) => ({ ...f, [campo]: valor }))

  const listaCategorias = useQuery({ queryKey: ['categorias', 'todas'], queryFn: categorias.todas, enabled: edicaoId !== null })
  const listaMarcas = useQuery({ queryKey: ['marcas', 'todas'], queryFn: marcas.todas, enabled: edicaoId !== null })

  async function abrir(produto: ProdutoResumo | 'novo') {
    salvar.reset()
    if (produto === 'novo') {
      setForm(vazio)
      setEdicaoId('novo')
      return
    }
    // A lista não traz a descrição: buscamos o produto completo antes de abrir a edição.
    try {
      const p = await produtos.obter(produto.id)
      setForm({
        codigo: p.codigo, nome: p.nome, descricao: p.descricao ?? '', unidadeMedida: p.unidadeMedida, categoriaId: p.categoriaId,
        marcaId: p.marcaId ?? '', precoVenda: numeroParaTexto(p.precoVenda), grupoEquivalencia: p.grupoEquivalencia ?? '',
      })
      setEdicaoId(p.id)
    } catch (e) {
      avisos.erro(e)
    }
  }

  const salvar = useMutation({
    mutationFn: () => {
      const r = {
        codigo: form.codigo, nome: form.nome, descricao: textoOuNulo(form.descricao), unidadeMedida: form.unidadeMedida,
        categoriaId: form.categoriaId, marcaId: form.marcaId || null, precoVenda: paraNumero(form.precoVenda),
        grupoEquivalencia: textoOuNulo(form.grupoEquivalencia),
      }
      return edicaoId === 'novo' || edicaoId === null ? produtos.criar(r) : produtos.atualizar(edicaoId, r)
    },
    onSuccess: () => {
      void clienteQuery.invalidateQueries({ queryKey: ['produtos'] })
      setEdicaoId(null)
      avisos.sucesso('Produto salvo.')
    },
  })
  const desativar = useMutation({
    mutationFn: (p: ProdutoResumo) => (p.ativo ? produtos.desativar(p.id) : produtos.reativar(p.id).then(() => undefined)),
    onSuccess: (_, p) => {
      void clienteQuery.invalidateQueries({ queryKey: ['produtos'] })
      avisos.sucesso(p.ativo ? 'Produto desativado.' : 'Produto reativado.')
    },
    onError: avisos.erro,
  })

  return (
    <>
      <TabelaCadastro<ProdutoResumo>
        titulo="Produtos"
        descricao="O preço de venda é o que o cliente paga no orçamento. Produto sem preço aparece como &quot;sem preço&quot;."
        chave="produtos"
        buscar={(f) => produtos.listar(f)}
        idDe={(p) => p.id}
        aoNovo={() => void abrir('novo')}
        textoNovo="Novo produto"
        aoClicar={(p) => void abrir(p)}
        comFiltroInativos
        colunas={[
          { titulo: 'Código', valor: (p) => p.codigo, largura: 110 },
          {
            titulo: 'Produto',
            valor: (p) => (
              <Box sx={{ opacity: p.ativo ? 1 : 0.5 }}>
                {p.nome} {!p.ativo && <Chip size="small" label="inativo" sx={{ ml: 1 }} />}
              </Box>
            ),
          },
          { titulo: 'Categoria', valor: (p) => p.categoriaNome ?? '' },
          { titulo: 'Marca', valor: (p) => p.marcaNome ?? '—' },
          {
            titulo: 'Preço de venda', alinhar: 'right',
            valor: (p) => (p.precoVenda === null ? <Chip size="small" color="warning" label="sem preço" /> : dinheiro(p.precoVenda)),
          },
          { titulo: 'Grupo', valor: (p) => p.grupoEquivalencia ?? '' },
        ]}
        acoes={(p) =>
          ehGerente && (
            <BotaoComConfirmacao
              icone={p.ativo ? <BlockIcon fontSize="small" /> : <RestoreIcon fontSize="small" />}
              dica={p.ativo ? 'Desativar' : 'Reativar'}
              perigoso={p.ativo}
              textoConfirmar={p.ativo ? 'Desativar' : 'Reativar'}
              pergunta={p.ativo
                ? `Desativar "${p.nome}"? Ele deixa de aparecer nos orçamentos, mas o histórico é mantido.`
                : `Reativar "${p.nome}"?`}
              aoConfirmar={() => desativar.mutate(p)} />
          )
        }
      />

      <DialogoFormulario aberto={edicaoId !== null} titulo={edicaoId === 'novo' ? 'Novo produto' : 'Editar produto'} largura="md"
        aoFechar={() => setEdicaoId(null)} aoSalvar={() => salvar.mutate()} salvando={salvar.isPending} erro={salvar.error}>
        <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 3fr' } }}>
          <TextField label="Código (SKU)" required autoFocus value={form.codigo} onChange={(e) => mudar('codigo', e.target.value)} />
          <TextField label="Nome" required value={form.nome} onChange={(e) => mudar('nome', e.target.value)} />
        </Box>
        <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '2fr 2fr 1fr' } }}>
          <TextField select label="Categoria" required value={form.categoriaId} onChange={(e) => mudar('categoriaId', e.target.value)}>
            {(listaCategorias.data?.itens ?? []).map((c) => <MenuItem key={c.id} value={c.id}>{c.nome}</MenuItem>)}
          </TextField>
          <TextField select label="Marca" value={form.marcaId} onChange={(e) => mudar('marcaId', e.target.value)}>
            <MenuItem value="">— Sem marca</MenuItem>
            {(listaMarcas.data?.itens ?? []).map((m) => <MenuItem key={m.id} value={m.id}>{m.nome}</MenuItem>)}
          </TextField>
          <TextField label="Unidade" required value={form.unidadeMedida} onChange={(e) => mudar('unidadeMedida', e.target.value.toUpperCase())}
            helperText="UN, CX, KG, M..." />
        </Box>
        <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 2fr' } }}>
          <TextField label="Preço de venda" value={form.precoVenda} onChange={(e) => mudar('precoVenda', e.target.value)}
            helperText="Vazio = sem preço"
            slotProps={{ input: { startAdornment: <InputAdornment position="start">R$</InputAdornment> }, htmlInput: { inputMode: 'decimal' } }} />
          <TextField label="Grupo de equivalência (opcional)" value={form.grupoEquivalencia}
            onChange={(e) => mudar('grupoEquivalencia', e.target.value)}
            helperText='Produtos com o mesmo grupo se substituem no orçamento comparativo. Ex.: "caderno 10 matérias"' />
        </Box>
        <TextField label="Descrição (opcional)" multiline minRows={2} value={form.descricao} onChange={(e) => mudar('descricao', e.target.value)} />
      </DialogoFormulario>
    </>
  )
}
