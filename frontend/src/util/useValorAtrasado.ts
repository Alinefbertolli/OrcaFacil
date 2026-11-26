import { useEffect, useState } from 'react'

/** Espera a pessoa parar de digitar (300 ms) antes de usar o valor: evita uma busca na API por tecla. */
export function useValorAtrasado<T>(valor: T, atraso = 300): T {
  const [atrasado, setAtrasado] = useState(valor)
  useEffect(() => {
    const temporizador = setTimeout(() => setAtrasado(valor), atraso)
    return () => clearTimeout(temporizador)
  }, [valor, atraso])
  return atrasado
}
