import { Box, TextField, Typography } from '@mui/material'
import type { EnderecoForm } from '../../util/formulario'

/** Os campos de endereço (todos opcionais), usados em clientes, fornecedores e na loja. */
export function CamposEndereco({ valor, aoMudar, desabilitado }: {
  valor: EnderecoForm
  aoMudar: (novo: EnderecoForm) => void
  desabilitado?: boolean
}) {
  const campo = (nome: keyof EnderecoForm, rotulo: string, extra?: { maiusculas?: boolean }) => (
    <TextField size="small" label={rotulo} value={valor[nome]} disabled={desabilitado}
      onChange={(e) => aoMudar({ ...valor, [nome]: extra?.maiusculas ? e.target.value.toUpperCase() : e.target.value })} />
  )
  return (
    <Box>
      <Typography variant="subtitle2" sx={{ mb: 1 }}>Endereço (opcional)</Typography>
      <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr', sm: '3fr 1fr' } }}>
        {campo('logradouro', 'Rua / avenida')}
        {campo('numero', 'Número')}
      </Box>
      <Box sx={{ display: 'grid', gap: 1.5, mt: 1.5, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' } }}>
        {campo('complemento', 'Complemento')}
        {campo('bairro', 'Bairro')}
      </Box>
      <Box sx={{ display: 'grid', gap: 1.5, mt: 1.5, gridTemplateColumns: { xs: '1fr', sm: '2fr 1fr 1.2fr' } }}>
        {campo('cidade', 'Cidade')}
        {campo('uf', 'UF', { maiusculas: true })}
        {campo('cep', 'CEP')}
      </Box>
    </Box>
  )
}
