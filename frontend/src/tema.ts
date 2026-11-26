import { createTheme } from '@mui/material/styles'

// Cores e fontes do sistema num lugar só. O azul é o mesmo do PDF do orçamento.
export const tema = createTheme({
  palette: {
    primary: { main: '#1F4E79' },
    secondary: { main: '#2E7D32' },
    background: { default: '#F4F6F8' },
  },
  shape: { borderRadius: 10 },
  typography: {
    fontFamily: '"Roboto", "Segoe UI", Arial, sans-serif',
    h5: { fontWeight: 600 },
    h6: { fontWeight: 600 },
  },
  components: {
    MuiButton: { defaultProps: { disableElevation: true } },
    MuiPaper: { defaultProps: { elevation: 0 }, styleOverrides: { root: { border: '1px solid #E0E4E8' } } },
    MuiAppBar: { styleOverrides: { root: { border: 'none' } } },
  },
})
