import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack } from '@mui/material'
import type { FormEvent, ReactNode } from 'react'
import { mensagemDeErro } from '../../api/cliente'

/**
 * Janela de cadastro/edição. É um <form>: apertar Enter em qualquer campo salva
 * (o mesmo comportamento que você pediu na Fase 10).
 */
export function DialogoFormulario(props: {
  aberto: boolean
  titulo: string
  aoFechar: () => void
  aoSalvar: () => void
  salvando: boolean
  erro?: unknown
  somenteLeitura?: boolean
  largura?: 'xs' | 'sm' | 'md'
  children: ReactNode
}) {
  function enviar(evento: FormEvent) {
    evento.preventDefault()
    if (!props.salvando && !props.somenteLeitura) props.aoSalvar()
  }

  return (
    <Dialog open={props.aberto} onClose={props.aoFechar} fullWidth maxWidth={props.largura ?? 'sm'}>
      <form onSubmit={enviar} noValidate>
        <DialogTitle>{props.titulo}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            {props.erro ? <Alert severity="error">{mensagemDeErro(props.erro)}</Alert> : null}
            {props.children}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={props.aoFechar}>{props.somenteLeitura ? 'Fechar' : 'Cancelar'}</Button>
          {!props.somenteLeitura && (
            <Button type="submit" variant="contained" disabled={props.salvando}>
              {props.salvando ? 'Salvando...' : 'Salvar'}
            </Button>
          )}
        </DialogActions>
      </form>
    </Dialog>
  )
}
