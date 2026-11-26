import { Pagination } from '@mui/material'
import type { Pagina } from '../api/tipos'

export function Paginacao<T>({ pagina, aoMudar }: { pagina: Pagina<T>; aoMudar: (numero: number) => void }) {
  if (pagina.totalPaginas <= 1) return null
  return <Pagination count={pagina.totalPaginas} page={pagina.pagina} onChange={(_, numero) => aoMudar(numero)} />
}
