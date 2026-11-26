/** Entrega um arquivo baixado da API ao usuário: cria um link temporário e "clica" nele. */
export function baixarArquivo(conteudo: Blob, nome: string) {
  const url = URL.createObjectURL(conteudo)
  const link = document.createElement('a')
  link.href = url
  link.download = nome
  document.body.appendChild(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 10_000) // libera a memória depois que o download começou
}
