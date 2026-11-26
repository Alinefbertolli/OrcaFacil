# OrçaFácil

Sistema inteligente de gestão e automação de orçamentos, oferecido como **SaaS multiempresa para varejistas de qualquer segmento** (papelarias, lojas de materiais, informática, utilidades...). Importa listas de produtos (PDF, imagem, Excel, Word, texto, WhatsApp), exige a revisão do usuário, cota preços em fontes rastreáveis e gera até três opções de orçamento (melhor custo-benefício, marcas reconhecidas, menor preço). Depois compara as opções, gera a proposta em PDF e integra a compra ao estoque.

> **Estado atual:** Fases 1 a 11 concluídas: domínio, banco, multiempresa (SaaS), API, login, cadastros, importação inteligente de listas (com OCR), motor de orçamentação (orçamento único ou comparativo com 3 opções), estoque (disponibilidade no orçamento e baixa ao finalizar a venda), envio ao cliente (PDF e texto pronto para o WhatsApp) e o frontend em React completo (fluxo de orçamento, cadastros, estoque e configurações), além da importação do catálogo por planilha do ERP (.xlsx/.csv, com prévia antes de gravar) e testes de integração com PostgreSQL real (316 testes no total). Próxima: Fase 13 (Dockerfiles, CI e documentação final).
> Plano completo e modelo de dados: [`docs/ARQUITETURA.md`](docs/ARQUITETURA.md).
> Estado do desenvolvimento e como continuar: [`docs/CONTEXTO.md`](docs/CONTEXTO.md).

## Tecnologias

- **Backend:** C# / .NET 10, ASP.NET Core Web API, EF Core 10, Swagger, JWT, FluentValidation
- **Banco:** PostgreSQL 17 (Npgsql)
- **Frontend (Fases 10 e 11):** React + TypeScript + Vite + MUI
- **Documentos:** PdfPig, ClosedXML, OpenXml, Tesseract (OCR), QuestPDF

## Estrutura

```
OrcaFacil/
├── backend/
│   ├── OrcaFacil.slnx
│   ├── OrcaFacil.Domain/          Entidades, enums, regras de negócio (sem dependências)
│   ├── OrcaFacil.Application/     Casos de uso, DTOs, validações, interfaces
│   ├── OrcaFacil.Infrastructure/  EF Core/PostgreSQL, integrações, arquivos
│   ├── OrcaFacil.API/             Controllers, autenticação, Swagger
│   ├── OrcaFacil.Tests/           Testes de unidade (rápidos, sem banco)
│   └── OrcaFacil.IntegrationTests/ Testes de integração (API + PostgreSQL reais, via Docker)
├── frontend/                      (Fases 10 e 11)
├── docs/ARQUITETURA.md
└── docker-compose.yml
```

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Docker (para o PostgreSQL) ou PostgreSQL 15+ instalado
- Node.js 22+ (a partir da Fase 10)

## Como executar

Todos os comandos `dotnet` rodam dentro da pasta **`backend`**.

### 1. Compilar e testar

```powershell
cd backend
dotnet build
dotnet test OrcaFacil.Tests              # testes de unidade: não precisam de banco nem de Docker
dotnet test OrcaFacil.IntegrationTests   # testes de integração: precisam do Docker Desktop ligado
dotnet test                              # todos (316 testes)
```

Os testes de integração criam sozinhos um PostgreSQL descartável (Testcontainers) e sobem a API em memória.
Eles **não usam** o seu banco de desenvolvimento nem os seus user-secrets.

### 2. Banco de dados

**Opção A — Docker Desktop:**
```powershell
copy .env.example .env          # na raiz; edite e defina POSTGRES_PASSWORD
docker compose up -d
```

**Opção B — PostgreSQL instalado no Windows** ([instalador oficial](https://www.postgresql.org/download/windows/)). Crie o usuário e o banco no pgAdmin ou no psql:
```sql
CREATE USER orcafacil WITH PASSWORD 'SUA_SENHA';
CREATE DATABASE orcafacil OWNER orcafacil;
```

### 3. Connection string (nunca no appsettings.json)

```powershell
cd backend
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Database=orcafacil;Username=orcafacil;Password=SUA_SENHA" --project OrcaFacil.API
```
Alternativa: variável de ambiente `ORCAFACIL_ConnectionStrings__Postgres`.

### 3.1 Chave JWT (assinatura dos tokens de login)

A API não inicia sem uma chave secreta de pelo menos 32 caracteres. Gere uma chave aleatória e grave-a no user-secrets (na pasta `backend`):

```powershell
$bytes = New-Object byte[] 64
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
dotnet user-secrets set "Jwt:Chave" ([Convert]::ToBase64String($bytes)) --project OrcaFacil.API
```

### 3.2 OCR: leitura de imagens e PDFs escaneados (opcional)

Para importar **fotos e imagens** de listas, o OCR (Tesseract) precisa do arquivo de idioma português. Na pasta `backend`:

```powershell
mkdir OrcaFacil.API\tessdata
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata_fast/raw/main/por.traineddata" -OutFile "OrcaFacil.API\tessdata\por.traineddata"
```

Sem esse arquivo, todo o resto da importação funciona (texto, WhatsApp, Excel, Word, PDF com texto); só as imagens respondem com uma mensagem explicando o que falta. Se aparecer erro de DLL, instale o **Microsoft Visual C++ Redistributable (x64)**.

### 4. Migrations

> ⚠️ O user-secrets só é lido no ambiente **Development**. Antes dos comandos `dotnet ef`, rode no mesmo terminal:
> `$env:ASPNETCORE_ENVIRONMENT = "Development"` (vale até fechar o terminal).

```powershell
dotnet tool install --global dotnet-ef      # apenas uma vez
dotnet ef migrations add Inicial -p OrcaFacil.Infrastructure -s OrcaFacil.API -o Persistencia/Migrations
dotnet ef database update -p OrcaFacil.Infrastructure -s OrcaFacil.API
```
Para cada mudança futura no modelo: `dotnet ef migrations add <NomeDescritivo>` com os mesmos parâmetros e depois `database update`.

### 5. Executar a API

```powershell
dotnet run --project OrcaFacil.API
```

- Swagger: https://localhost:7080/swagger
- Health check (inclui o banco): https://localhost:7080/health → `Healthy`
- Logs: no terminal e em arquivos `logs/orcafacil-AAAAMMDD.log` (pasta `backend/OrcaFacil.API/logs`)

### 6. Executar o frontend (React)

Requer **Node.js 22.22+** (`node --version`). Com a API rodando em outro terminal:

```powershell
cd frontend
npm install        # só na primeira vez (ou quando o package.json mudar)
npm run dev        # http://localhost:5173
```

- O Vite repassa as chamadas `/api` para a API em `https://localhost:7080` (proxy em `vite.config.ts`), então não é preciso configurar CORS.
- `npm run build` confere os tipos (TypeScript) e gera a versão de produção em `frontend/dist`.
- `npm run lint` roda o Oxlint.

## Regras de negócio já implementadas no domínio

- **Revisão obrigatória:** uma lista importada só gera orçamento depois de revisada e confirmada.
- **Origem do preço:** todo preço carrega `OrigemPreco` (Integração, Fornecedor, Manual, Estimado, Simulado). Opções com preço não cotado são sinalizadas.
- **Totais calculados no domínio**, com arredondamento comercial. A API nunca recebe totais do cliente.
- **Máquina de status do orçamento** (Em elaboração → Aguardando aprovação → Aprovado/Reprovado → Finalizado). Só orçamentos em elaboração podem ser editados.
- **Economia:** opção mais cara menos a opção selecionada.
- **Duplicação** de orçamentos com rastreio da origem.
- **Estoque:** entrada, saída (bloqueia saldo negativo), ajuste de inventário com motivo obrigatório. Toda alteração gera uma movimentação imutável. Alerta de estoque mínimo.
- **Validação de CPF/CNPJ** pelos dígitos verificadores.

## Endpoints

A lista completa planejada está em [`docs/ARQUITETURA.md` §5](docs/ARQUITETURA.md#5-principais-endpoints-rest-prefixo-apiv1). Este README é atualizado a cada fase com os endpoints já disponíveis.

| Disponível | Endpoint |
|---|---|
| ✅ | `GET /health` |
| ✅ | `GET /api/v1/sistema/info` |
| ✅ (só Development) | `GET /api/v1/sistema/demonstracao-erro/{tipo}` |
| ✅ público | `POST /api/v1/auth/registrar` · `login` · `refresh` · `logout` · `esqueci-senha` · `redefinir-senha` |
| ✅ logado | `GET/PUT /api/v1/usuarios/me` · `PUT /api/v1/usuarios/me/senha` |
| ✅ Gerente | `GET /api/v1/usuarios` · `GET /api/v1/usuarios/{id}` |
| ✅ Administrador | `POST /api/v1/usuarios` · `PATCH /api/v1/usuarios/{id}/perfil` · `PATCH /api/v1/usuarios/{id}/situacao` |

| ✅ logado | `GET/POST /api/v1/categorias` · `GET/PUT /api/v1/categorias/{id}` (idem para `marcas`, `produtos`, `fornecedores`, `clientes`) |
| ✅ logado | `PUT /api/v1/produtos/{id}/fornecedores/{fornecedorId}` · `GET /api/v1/produtos/{id}/historico-precos` · `GET /api/v1/fornecedores/{id}/produtos` |
| ✅ Gerente | `DELETE` dos cadastros (física: categorias e marcas; lógica: produtos, fornecedores, clientes) · `POST .../{id}/reativar` · `DELETE /api/v1/produtos/{id}/fornecedores/{fornecedorId}` |

| ✅ logado | `POST /api/v1/importacoes/texto` · `POST /api/v1/importacoes/arquivo` (multipart) · `GET /api/v1/importacoes` · `GET /api/v1/importacoes/{id}` |
| ✅ logado | Revisão: `PUT/DELETE /api/v1/importacoes/{id}/itens/{itemId}` · `POST /api/v1/importacoes/{id}/itens` · `GET /api/v1/importacoes/{id}/itens/{itemId}/sugestoes` · `POST /api/v1/importacoes/{id}/confirmar-revisao` |

| ✅ logado | `POST /api/v1/orcamentos/gerar` · `GET /api/v1/orcamentos` · `GET /api/v1/orcamentos/{id}` · `GET /api/v1/orcamentos/{id}/comparacao` |
| ✅ logado | `PUT /api/v1/orcamentos/{id}/opcoes/{opcaoId}/itens/{itemId}/preco` · `POST /api/v1/orcamentos/{id}/opcoes/{opcaoId}/selecionar` · `POST /api/v1/orcamentos/{id}/status` |
| ✅ logado | `GET /api/v1/orcamentos/{id}/pdf` (PDFsharp/MigraDoc, MIT) · `GET /api/v1/orcamentos/{id}/whatsapp` (texto + link wa.me) |
| ✅ logado / Administrador | `GET /api/v1/empresa/configuracoes` · `PUT /api/v1/empresa/configuracoes` (Administrador) |

| ✅ logado | `GET /api/v1/estoque` (`abaixoDoMinimo`, `busca`) · `GET /api/v1/estoque/{produtoId}` · `POST .../entradas` · `POST .../saidas` · `GET .../movimentacoes` |
| ✅ Gerente | `POST /api/v1/estoque/{produtoId}/ajustes` · `PUT /api/v1/estoque/{produtoId}/configuracao` |

> ⚠️ O envio de e-mail (recuperação de senha) ainda é **simulado**: o link e o token aparecem no log da API.
| ✅ | `GET /swagger` |
