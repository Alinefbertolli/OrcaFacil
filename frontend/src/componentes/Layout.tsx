import CategoryIcon from '@mui/icons-material/Category'
import DescriptionIcon from '@mui/icons-material/Description'
import InventoryIcon from '@mui/icons-material/Inventory2'
import ListAltIcon from '@mui/icons-material/ListAlt'
import LocalShippingIcon from '@mui/icons-material/LocalShipping'
import LogoutIcon from '@mui/icons-material/Logout'
import PeopleIcon from '@mui/icons-material/People'
import PostAddIcon from '@mui/icons-material/PostAdd'
import SellIcon from '@mui/icons-material/Sell'
import SettingsIcon from '@mui/icons-material/Settings'
import UploadFileIcon from '@mui/icons-material/UploadFile'
import WarehouseIcon from '@mui/icons-material/Warehouse'
import {
  AppBar,
  Box,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  ListSubheader,
  Toolbar,
  Tooltip,
  Typography,
} from '@mui/material'
import type { ReactNode } from 'react'
import { NavLink, Outlet } from 'react-router'
import { useAutenticacao } from '../auth/useAutenticacao'

const LARGURA_MENU = 230

interface ItemMenu {
  para: string
  texto: string
  icone: ReactNode
}

/** O menu em grupos: o dia a dia (atendimento) primeiro, depois cadastros e a administração. */
const grupos: { titulo: string; itens: ItemMenu[] }[] = [
  {
    titulo: 'Atendimento',
    itens: [
      { para: '/listas/nova', texto: 'Nova lista', icone: <PostAddIcon /> },
      { para: '/listas', texto: 'Listas recebidas', icone: <ListAltIcon /> },
      { para: '/orcamentos', texto: 'Orçamentos', icone: <DescriptionIcon /> },
    ],
  },
  {
    titulo: 'Cadastros',
    itens: [
      { para: '/produtos', texto: 'Produtos', icone: <InventoryIcon /> },
      { para: '/produtos/importar', texto: 'Importar planilha', icone: <UploadFileIcon /> },
      { para: '/categorias', texto: 'Categorias', icone: <CategoryIcon /> },
      { para: '/marcas', texto: 'Marcas', icone: <SellIcon /> },
      { para: '/clientes', texto: 'Clientes', icone: <PeopleIcon /> },
      { para: '/fornecedores', texto: 'Fornecedores', icone: <LocalShippingIcon /> },
    ],
  },
  {
    titulo: 'Loja',
    itens: [
      { para: '/estoque', texto: 'Estoque', icone: <WarehouseIcon /> },
      { para: '/configuracoes', texto: 'Configurações', icone: <SettingsIcon /> },
    ],
  },
]

export function Layout() {
  const { usuario, sair } = useAutenticacao()

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar position="fixed" sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>
            OrçaFácil
          </Typography>
          <Box sx={{ textAlign: 'right', mr: 1 }}>
            <Typography variant="body2">{usuario?.nome}</Typography>
            <Typography variant="caption" sx={{ opacity: 0.8 }}>
              {usuario?.empresaNome}
            </Typography>
          </Box>
          <Tooltip title="Sair">
            <IconButton color="inherit" onClick={() => void sair()} aria-label="Sair">
              <LogoutIcon />
            </IconButton>
          </Tooltip>
        </Toolbar>
      </AppBar>

      <Drawer
        variant="permanent"
        sx={{ width: LARGURA_MENU, flexShrink: 0, '& .MuiDrawer-paper': { width: LARGURA_MENU, boxSizing: 'border-box' } }}
      >
        <Toolbar />
        {grupos.map((grupo) => (
          <List key={grupo.titulo} dense subheader={<ListSubheader>{grupo.titulo}</ListSubheader>}>
            {grupo.itens.map((item) => (
              <ListItemButton
                key={item.para}
                component={NavLink}
                to={item.para}
                end
                sx={{ '&.active': { bgcolor: 'action.selected', fontWeight: 600 } }}
              >
                <ListItemIcon>{item.icone}</ListItemIcon>
                <ListItemText primary={item.texto} />
              </ListItemButton>
            ))}
          </List>
        ))}
      </Drawer>

      <Box component="main" sx={{ flexGrow: 1, p: 3, minWidth: 0 }}>
        <Toolbar />
        <Outlet />
      </Box>
    </Box>
  )
}
