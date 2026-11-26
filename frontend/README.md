# OrçaFácil — frontend

Telas do OrçaFácil em **React 19 + TypeScript + Vite**, com **Material UI**, **React Router** e **TanStack Query** (todas com licença MIT).

```powershell
npm install
npm run dev      # http://localhost:5173 (a API precisa estar rodando em https://localhost:7080)
npm run build    # confere os tipos e gera a versão de produção em dist/
npm run lint     # Oxlint
```

## Estrutura

```
src/
├── api/          cliente HTTP (token, renovação automática, erros), endpoints e tipos da API
├── auth/         contexto de login e rota protegida
├── componentes/  layout, seletores de produto/cliente, paginação, carregando
├── paginas/      Login, NovaLista, RevisaoLista, Listas, Orcamentos, OrcamentoDetalhe
├── util/         formatação brasileira (R$, datas) e rótulos de situação
├── tema.ts       cores e fontes
└── main.tsx      provedores (tema, React Query, rotas, autenticação)
```

Veja também `docs/GUIA-DO-ESTUDANTE.md`, seção 4I.
