import { Navigate, Route, Routes } from 'react-router'
import { RotaProtegida } from './auth/RotaProtegida'
import { Layout } from './componentes/Layout'
import { Listas } from './paginas/Listas'
import { Login } from './paginas/Login'
import { NovaLista } from './paginas/NovaLista'
import { OrcamentoDetalhe } from './paginas/OrcamentoDetalhe'
import { Orcamentos } from './paginas/Orcamentos'
import { RevisaoLista } from './paginas/RevisaoLista'

/** Mapa de telas: cada endereço (URL) mostra uma página. */
export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />

      {/* Tudo aqui dentro exige login e usa a moldura com menu */}
      <Route element={<RotaProtegida />}>
        <Route element={<Layout />}>
          <Route index element={<Navigate to="/listas/nova" replace />} />
          <Route path="/listas/nova" element={<NovaLista />} />
          <Route path="/listas" element={<Listas />} />
          <Route path="/listas/:id" element={<RevisaoLista />} />
          <Route path="/orcamentos" element={<Orcamentos />} />
          <Route path="/orcamentos/:id" element={<OrcamentoDetalhe />} />
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
