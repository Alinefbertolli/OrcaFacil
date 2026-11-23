# Instruções para o Claude

Projeto OrçaFácil (ASP.NET Core 10 + EF Core + PostgreSQL + React/TS).

## Sobre a usuária (importante)
- Estudante do **1º semestre de Análise e Desenvolvimento de Sistemas**. É o primeiro projeto dela com agente de IA.
- **Modo didático obrigatório:** passo a passo numerado, UMA ação por passo, dizendo o resultado esperado (✅) e o que fazer se der errado (❌). Explicar cada etapa em detalhe, em português simples: **o que** estamos fazendo, **por que**, e **como fazer** (comandos um por um, dizendo o que cada um faz e onde executar). Usar analogias, mostrar trechos de código explicados linha a linha e ligar os conceitos às matérias do curso (POO, Banco de Dados, Engenharia de Software).
- Ao concluir cada fase, acrescentar uma seção em `docs/GUIA-DO-ESTUDANTE.md`.
- Ambiente: Windows, PowerShell, VS Code, .NET SDK 10.0.302. Disco C: com pouco espaço. **Escolheu usar Docker para aprender** (o PostgreSQL roda em contêiner via `docker-compose.yml`). Em 15/11/2026 o Docker Desktop ainda estava sendo instalado.

## Regras do projeto
- Antes de qualquer trabalho, leia `docs/CONTEXTO.md` (estado atual, decisões e próxima fase) e `docs/ARQUITETURA.md` (modelo de dados, endpoints e fluxo).
- Ao terminar cada sessão ou fase, atualize `docs/CONTEXTO.md`: a tabela de fases, as decisões novas e o histórico de sessões.
- Idioma do código e da documentação: português (nomes de domínio em PT-BR, sem acentos nos identificadores).
- Não use bibliotecas com licença comercial paga sem avisar (ex.: FluentAssertions 8+; use AwesomeAssertions).
- Não avance de fase com erro de compilação ou teste falhando.
