import { Alert, Snackbar } from '@mui/material'
import { useCallback, useMemo, useState, type ReactNode } from 'react'
import { mensagemDeErro } from '../../api/cliente'
import { AvisosContexto } from './contexto'

interface Aviso {
  tipo: 'success' | 'error'
  texto: string
  chave: number
}

export function AvisosProvider({ children }: { children: ReactNode }) {
  const [aviso, setAviso] = useState<Aviso | null>(null)

  const sucesso = useCallback((texto: string) => setAviso({ tipo: 'success', texto, chave: Date.now() }), [])
  const erro = useCallback(
    (e: unknown) => setAviso({ tipo: 'error', texto: typeof e === 'string' ? e : mensagemDeErro(e), chave: Date.now() }),
    [],
  )
  const valor = useMemo(() => ({ sucesso, erro }), [sucesso, erro])

  return (
    <AvisosContexto value={valor}>
      {children}
      <Snackbar key={aviso?.chave} open={aviso !== null} onClose={() => setAviso(null)}
        autoHideDuration={aviso?.tipo === 'error' ? 8000 : 3000} anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}>
        <Alert severity={aviso?.tipo ?? 'success'} variant="filled" onClose={() => setAviso(null)}>
          {aviso?.texto}
        </Alert>
      </Snackbar>
    </AvisosContexto>
  )
}
