import { Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, IconButton, Tooltip } from '@mui/material'
import { useState, type ReactNode } from 'react'

/** Botão (ícone) que pergunta "tem certeza?" antes de fazer algo que não dá para desfazer facilmente. */
export function BotaoComConfirmacao(props: {
  icone: ReactNode
  dica: string
  pergunta: string
  textoConfirmar: string
  aoConfirmar: () => void
  perigoso?: boolean
  desabilitado?: boolean
}) {
  const [aberto, setAberto] = useState(false)
  return (
    <>
      <Tooltip title={props.dica}>
        <span>
          <IconButton size="small" disabled={props.desabilitado} onClick={() => setAberto(true)}>
            {props.icone}
          </IconButton>
        </span>
      </Tooltip>
      <Dialog open={aberto} onClose={() => setAberto(false)}>
        <DialogTitle>Confirmar</DialogTitle>
        <DialogContent>
          <DialogContentText>{props.pergunta}</DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAberto(false)}>Cancelar</Button>
          <Button variant="contained" color={props.perigoso ? 'error' : 'primary'} autoFocus
            onClick={() => { setAberto(false); props.aoConfirmar() }}>
            {props.textoConfirmar}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  )
}
