import DescriptionIcon from '@mui/icons-material/Description'
import InventoryIcon from '@mui/icons-material/Inventory2'
import ListAltIcon from '@mui/icons-material/ListAlt'
import LogoutIcon from '@mui/icons-material/Logout'
import PostAddIcon from '@mui/icons-material/PostAdd'
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
import { NavLink, Outlet } from 'react-router'
import { useAutenticacao } from '../auth/useAutenticacao'

const LARGURA_MENU = 230

const menu = [
  { para: '/listas/nova', texto: 'Nova lista', icone: <PostAddIcon /> },
  { para: '/listas', texto: 'Listas recebidas', icone: <ListAltIcon /> },
  { para: '/orcamentos', texto: 'Orçamentos', icone: <DescriptionIcon /> },
]

/** Moldura das telas internas: barra no topo, menu à esquerda e o conteúdo da página no meio (<Outlet />). */
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
        <List>
          {menu.map((item) => (
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
        <List subheader={<ListSubheader>Em breve (Fase 11)</ListSubheader>}>
          <ListItemButton disabled>
            <ListItemIcon>
              <InventoryIcon />
            </ListItemIcon>
            <ListItemText primary="Cadastros e estoque" />
          </ListItemButton>
        </List>
      </Drawer>

      <Box component="main" sx={{ flexGrow: 1, p: 3, minWidth: 0 }}>
        <Toolbar />
        <Outlet />
      </Box>
    </Box>
  )
}
