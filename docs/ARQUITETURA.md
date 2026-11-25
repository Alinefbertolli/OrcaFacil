# OrçaFácil — Análise de Requisitos e Arquitetura (Fase 1)

> Documento vivo. Cada fase concluída atualiza as seções correspondentes.

## 0. Atualização de 18/11/2026: foco no cliente final

O orçamento é a **resposta da loja ao cliente final** (a mãe com a lista escolar, o pedreiro com a lista de obra, a cliente do catálogo de roupas). O preço é o **preço de venda** da loja. As 3 opções comparam **produtos equivalentes do próprio catálogo** (marcas e preços diferentes), não fornecedores. Existe também o **orçamento único**, para itens exatos. Os fornecedores e os custos são informação interna. Onde as seções abaixo falam em "cotação de fornecedores" e "provedores de preço", leia como recurso secundário (reposição e margem); o motor da Fase 7 segue a seção "Visão do produto" do `CONTEXTO.md`.

## 1. Visão geral

O OrçaFácil é um **SaaS multiempresa para varejistas de qualquer segmento** (papelarias, lojas de materiais, informática, utilidades...). Ele recebe listas de produtos (PDF, imagem, Excel, Word, texto, WhatsApp), interpreta os itens, **obriga a revisão humana**, consulta preços em fontes rastreáveis e gera até três opções de orçamento (custo-benefício, marcas reconhecidas, menor preço). Depois as compara e as transforma em proposta (PDF), compra e movimentação de estoque.

### Princípios que guiam as decisões

| Princípio | Consequência prática |
|---|---|
| **Nenhum preço sem origem** | Todo preço carrega `OrigemPreco` (Integração, Fornecedor cadastrado, Manual, Estimado, Simulado). A UI exibe a origem como selo. Preço "Simulado" nunca aparece sem esse selo. |
| **Revisão obrigatória** | Uma `ListaImportada` só gera orçamento quando está em `Revisada`. A regra fica no domínio, não só na tela. |
| **Integrações isoladas** | Provedores de preço implementam `IProvedorPrecos`. A aplicação não conhece nenhum provedor concreto. O primeiro provedor é **simulado e identificado como tal**. |
| **Isolamento entre empresas** | Cada varejista (Empresa) só enxerga os próprios dados. Garantido no `DbContext` por filtro global (`WHERE EmpresaId = empresa logada`) e por validação ao salvar, não depende de cada consulta "lembrar" do filtro. |
| **Neutro quanto ao segmento** | Nenhuma regra, categoria ou unidade é específica de um ramo. O `Segmento` da empresa serve só para sugestões. |
| **Dinheiro é `decimal`** | Nunca `double`. Colunas `numeric(18,4)` para preço unitário e `numeric(18,2)` para totais. |
| **Domínio rico onde há regra** | Estoque, Orçamento, Opções e Importação têm métodos que garantem invariantes. Cadastros simples (Cliente, Categoria) usam validação na camada Application (FluentValidation). |

## 2. Arquitetura

Arquitetura em camadas no estilo *Clean Architecture*: as dependências apontam sempre para dentro.

```
┌──────────────────────────────────────────────────────────────┐
│ Frontend (React + TS + MUI)  ──HTTP/JSON + JWT──┐            │
└─────────────────────────────────────────────────┼────────────┘
                                                  ▼
┌──────────────────────────────────────────────────────────────┐
│ OrcaFacil.API  — Controllers, autenticação JWT, Swagger,      │
│                  middleware de exceções, rate limit, CORS     │
└───────────────┬──────────────────────────────────────────────┘
                ▼
┌──────────────────────────────────────────────────────────────┐
│ OrcaFacil.Application — Casos de uso (serviços), DTOs,        │
│   validações (FluentValidation), interfaces de portas:        │
│   IProvedorPrecos, IExtratorDocumento, IServicoOcr,           │
│   IGeradorPdf, IInterpretadorLista, repositórios              │
│   Motor de orçamentação (estratégias)                         │
└───────────────┬──────────────────────────────────────────────┘
                ▼
┌──────────────────────────────────────────────────────────────┐
│ OrcaFacil.Domain — Entidades, enums, regras de negócio,       │
│   exceções de domínio. Sem dependência de framework.          │
└──────────────────────────────────────────────────────────────┘
                ▲
┌───────────────┴──────────────────────────────────────────────┐
│ OrcaFacil.Infrastructure — EF Core + Npgsql, repositórios,     │
│   migrations, extratores (PdfPig, ClosedXML, OpenXml),         │
│   OCR (Tesseract), PDF (QuestPDF), provedores de preço,        │
│   hash de senha, envio de e-mail                               │
└──────────────────────────────────────────────────────────────┘
```

`Infrastructure` implementa as interfaces declaradas em `Application`. A `API` faz a composição (injeção de dependência).

### Decisões tecnológicas

| Necessidade | Escolha | Motivo |
|---|---|---|
| Runtime | .NET 10 (LTS) | Versão de suporte longo vigente |
| ORM / banco | EF Core 10 + Npgsql | Requisito; migrations; consultas parametrizadas (proteção contra SQL injection) |
| Validação | FluentValidation | Regras testáveis e separadas dos DTOs |
| Hash de senha | `PasswordHasher<T>` do ASP.NET Core Identity (PBKDF2 com salt e iterações) | Padrão mantido pela Microsoft, sem trazer o Identity inteiro |
| Autenticação | JWT Bearer (access token curto) + refresh token rotativo salvo com hash | Logout real e revogação |
| Logs | Serilog (console + arquivo) + tabela `LogSistema` para auditoria de negócio | Log técnico separado da trilha de auditoria |
| Leitura de PDF | UglyToad.PdfPig | Extração de texto; PDFs escaneados vão para OCR |
| Excel | ClosedXML | Leitura de .xlsx sem Office instalado |
| Word | DocumentFormat.OpenXml | Leitura de .docx |
| OCR | Tesseract (`por` + `eng`) por trás de `IServicoOcr` | Gratuito e local. Pode ser trocado por Azure/Google Vision sem afetar o resto |
| Geração de PDF | QuestPDF | API fluente em C#. **Licença Community gratuita até US$ 1 mi de receita anual; acima disso é paga** |
| Testes | xUnit + AwesomeAssertions + Testcontainers (PostgreSQL real nos testes de integração) | AwesomeAssertions é o fork gratuito (Apache 2.0) do FluentAssertions, com a mesma API; o FluentAssertions 8+ passou a ser pago para uso comercial |
| Frontend | Vite + React + TypeScript + MUI + React Router + TanStack Query + React Hook Form/Zod + Recharts | Biblioteca de componentes madura, cache de API e formulários tipados |

## 3. Estrutura de pastas

```
OrcaFacil/
├── backend/
│   ├── OrcaFacil.slnx
│   ├── Directory.Build.props
│   ├── OrcaFacil.Domain/
│   │   ├── Comum/            EntidadeBase, DomainException
│   │   ├── Enums/            Status, perfis, origens de preço...
│   │   └── Entidades/        Usuario, Produto, Orcamento, Estoque...
│   ├── OrcaFacil.Application/
│   │   ├── Abstracoes/       Repositórios, IUnitOfWork, portas externas
│   │   ├── Autenticacao/     Casos de uso + DTOs + validadores
│   │   ├── Produtos/ Fornecedores/ Clientes/ Estoque/ ...
│   │   ├── Importacao/       Interpretador de linhas, orquestração
│   │   └── Orcamentacao/     Motor + estratégias + comparação
│   ├── OrcaFacil.Infrastructure/
│   │   ├── Persistencia/     DbContext, Configurations, Migrations, Repositórios
│   │   ├── Documentos/       Extratores PDF/Excel/Word, OCR
│   │   ├── Pdf/              Gerador de proposta (QuestPDF)
│   │   ├── Precos/           Provedores (Simulado, futuros reais)
│   │   └── Seguranca/        Hash, emissão de JWT
│   ├── OrcaFacil.API/
│   │   ├── Controllers/
│   │   ├── Middlewares/      Tratamento global de exceções
│   │   └── Program.cs
│   └── OrcaFacil.Tests/
│       ├── Domain/  Application/  Integration/
├── frontend/
│   └── src/ { api/, components/, pages/, hooks/, routes/, store/, types/ }
├── docs/
│   └── ARQUITETURA.md
├── docker-compose.yml        PostgreSQL local
└── README.md
```

## 4. Modelo de dados

Convenções: PK `Id uuid`; **toda tabela de dados do cliente tem `EmpresaId` (FK → Empresas, obrigatório)**, exceto filhos de agregado (itens de lista, opções e itens de orçamento), que herdam a empresa pela raiz, e tokens de autenticação, que pertencem ao usuário; `CriadoEm`/`AtualizadoEm timestamptz` (UTC) em todas as tabelas, preenchidas automaticamente pelo `DbContext`. Exclusão lógica (`Ativo`) em cadastros referenciados por histórico (Produto, Fornecedor, Cliente, Usuario), para não quebrar orçamentos antigos.

### Ajustes em relação à lista de entidades do briefing

- **`OpcaoOrcamento` (nova).** Um orçamento para o cliente tem até 3 opções e o usuário escolhe uma. Sem essa entidade, as 3 opções viram 3 orçamentos soltos, e numeração, status e cliente ficam triplicados. Com ela: `Orcamento` (cabeçalho: número, cliente, status, validade) 1→N `OpcaoOrcamento` (tipo, totais, selecionada) 1→N `ItemOrcamento`.
- **`Marca` (nova).** A estratégia "marcas reconhecidas" precisa de um critério. Em vez de o sistema "achar" que uma marca é boa, o usuário classifica o `NivelReconhecimento` (1–5) de cada marca. É um dado explícito e auditável.
- **`RefreshToken` (nova).** Necessária para logout e revogação com JWT.
- **`TokenRecuperacaoSenha` (nova).** Token de uso único, com hash e expiração.

### Tabelas

**Empresa** (cliente do SaaS): Id, RazaoSocial (200, obrig.), NomeFantasia, Cnpj (14, **único**, validado), Segmento (enum: Outro, Papelaria, MaterialConstrucao, Informatica, Eletronicos, Utilidades, Alimentacao, Farmacia, Vestuario, Autopecas), Ativa.

**ContadorNumeracao** (técnica): PK (EmpresaId, Chave), Ultimo. Gera `ORC-AAAA-NNNNNN` por empresa e por ano com `INSERT ... ON CONFLICT DO UPDATE ... RETURNING` (atômico).

> Unicidades de negócio passam a valer **dentro de cada empresa**: (EmpresaId, Codigo) em Produto; (EmpresaId, Nome) em Categoria e Marca; (EmpresaId, Cnpj) em Fornecedor; (EmpresaId, Documento) em Cliente; (EmpresaId, Numero) em Orcamento. O e-mail do usuário é único no sistema inteiro (login global).

**Usuario**: Id, Nome (150, obrig.), Email (200, obrig., **único**), SenhaHash (obrig.), Perfil (enum: Administrador, Gerente, Operador), Ativo, UltimoLoginEm.

**RefreshToken**: Id, UsuarioId (FK), TokenHash (único), ExpiraEm, RevogadoEm, SubstituidoPorId.

**TokenRecuperacaoSenha**: Id, UsuarioId (FK), TokenHash (único), ExpiraEm, UsadoEm.

**Cliente**: Id, Nome (200, obrig.), TipoPessoa (Fisica/Juridica), Documento (CPF/CNPJ só dígitos, **único quando informado**), Telefone, Email, Endereço (owned: Logradouro, Numero, Complemento, Bairro, Cidade, UF, CEP), Observacoes, Ativo.

**Categoria**: Id, Nome (100, **único**), Descricao, CategoriaPaiId (FK opcional, autorrelacionamento).

**Marca**: Id, Nome (100, **único**), NivelReconhecimento (1–5, check constraint).

**Produto**: Id, Codigo (50, **único**, ex.: SKU interno), Nome (200, obrig.), Descricao, UnidadeMedida (obrig., ex.: UN, KG, M, M², CX), CategoriaId (FK), MarcaId (FK opcional), PrecoReferencia (numeric 18,4, opcional), OrigemPrecoReferencia, Ativo. Índice GIN trigram em `Nome` (extensão `pg_trgm`) para busca aproximada na importação.

**Fornecedor**: Id, RazaoSocial (200, obrig.), NomeFantasia, Cnpj (14 dígitos, **único**, validado com dígito verificador), Contato, Telefone, Email, Endereço (owned), Avaliacao (numeric 3,2, 0–5), Ativo.

**ProdutoFornecedor** (N:N com atributos): Id, ProdutoId (FK), FornecedorId (FK), CodigoNoFornecedor, PrecoAtual (18,4), PrazoEntregaDias, Disponivel, AtualizadoPrecoEm. **Único (ProdutoId, FornecedorId).**

**HistoricoPreco**: Id, ProdutoId (FK), FornecedorId (FK opcional), Preco (18,4), Origem (enum), RegistradoEm. Índice (ProdutoId, RegistradoEm desc).

**Cotacao**: resultado bruto e normalizado de uma consulta de preço. Id, ProdutoId (FK opc.), FornecedorId (FK opc.), Provedor (nome do integrador), TermoPesquisa, DescricaoEncontrada, MarcaEncontrada, PrecoUnitario, Frete, PrazoEntregaDias, Disponivel, Origem, ConsultadoEm, ValidaAte, UrlOferta.

**ListaImportada**: Id, UsuarioId (FK), Formato (Pdf, Imagem, Excel, Word, Texto, WhatsApp), NomeArquivoOriginal, CaminhoArmazenamento, TextoExtraido, Status (Processando, AguardandoRevisao, Revisada, Erro), MensagemErro, ClienteId (FK opc.).

**ItemListaImportada**: Id, ListaImportadaId (FK, cascade), Ordem, TextoOriginal, Descricao, Quantidade (18,3), UnidadeMedida, Marca, ProdutoId (FK opc., produto do catálogo associado), ConfiancaInterpretacao (0–1), Excluido.

**Orcamento**: Id, Numero (único por empresa, `ORC-AAAA-000001`, reinicia a cada ano), ClienteId (FK opc.), UsuarioId (FK), ListaImportadaId (FK opc.), Status (EmElaboracao, AguardandoAprovacao, Aprovado, Reprovado, Finalizado), ValidadeAte, CondicoesComerciais, Observacoes, OrcamentoOrigemId (FK opc., autorrelacionamento para duplicação).

**OpcaoOrcamento**: Id, OrcamentoId (FK, cascade), Tipo (CustoBeneficio, MarcasReconhecidas, MenorPreco, Manual), Subtotal, FreteTotal, ValorTotal, Selecionada. **Único (OrcamentoId, Tipo).** Índice parcial garantindo no máximo uma `Selecionada = true` por orçamento.

**ItemOrcamento**: Id, OpcaoOrcamentoId (FK, cascade), ProdutoId (FK opc.), CotacaoId (FK opc.), FornecedorId (FK opc.), Descricao, Marca, Quantidade, UnidadeMedida, PrecoUnitario, PrecoTotal, Frete, PrazoEntregaDias, Disponivel, OrigemPreco.

**Estoque**: Id, ProdutoId (FK, **único**: 1:1), QuantidadeDisponivel (18,3, ≥ 0), EstoqueMinimo (≥ 0), Localizacao.

**MovimentacaoEstoque**: Id, EstoqueId (FK), Tipo (Entrada, Saida, Ajuste), Quantidade (> 0), SaldoAnterior, SaldoPosterior, Motivo, OrcamentoId (FK opc., compra originada de orçamento), UsuarioId (FK). Somente inserção (imutável). Índice (EstoqueId, CriadoEm).

**LogSistema**: Id, UsuarioId (FK opc.), Nivel, Acao, Entidade, EntidadeId, Detalhes (jsonb), Ip, CriadoEm.

### Relacionamentos

```
Empresa 1──N (todas as tabelas de dados do cliente)
Usuario 1──N Orcamento, ListaImportada, MovimentacaoEstoque, RefreshToken
Cliente 1──N Orcamento
Categoria 1──N Produto ; Categoria 1──N Categoria (subcategorias)
Marca 1──N Produto
Produto N──N Fornecedor   (via ProdutoFornecedor)
Produto 1──N HistoricoPreco, Cotacao ; Produto 1──1 Estoque
Fornecedor 1──N HistoricoPreco, Cotacao, ItemOrcamento
ListaImportada 1──N ItemListaImportada ; ListaImportada 1──N Orcamento
Orcamento 1──N OpcaoOrcamento 1──N ItemOrcamento
Estoque 1──N MovimentacaoEstoque ; Orcamento 1──N MovimentacaoEstoque
```

Exclusões: `Cascade` apenas em composições (itens de lista, opções/itens de orçamento). Nos demais casos, `Restrict`: não se apaga um fornecedor com histórico, ele é desativado.

## 5. Principais endpoints (REST, prefixo `/api/v1`)

| Recurso | Endpoints |
|---|---|
| Autenticação | `POST /auth/registrar` (cria Empresa + administrador) · `POST /auth/login` · `POST /auth/refresh` · `POST /auth/logout` · `POST /auth/esqueci-senha` · `POST /auth/redefinir-senha` |
| Usuários | `GET /usuarios/me` · `PUT /usuarios/me` · `PUT /usuarios/me/senha` · `GET/POST/PUT /usuarios` (Admin) |
| Dashboard | `GET /dashboard/indicadores` · `GET /dashboard/desempenho-mensal?meses=12` |
| Produtos | `GET /produtos?busca=&categoriaId=&pagina=` · `GET /produtos/{id}` · `POST` · `PUT /{id}` · `DELETE /{id}` · `GET /produtos/{id}/historico-precos` · `PUT /produtos/{id}/fornecedores/{fornecedorId}` |
| Categorias / Marcas | CRUD em `/categorias` e `/marcas` |
| Fornecedores | CRUD em `/fornecedores` · `GET /fornecedores/{id}/produtos` · `GET /fornecedores/{id}/historico-precos` · `PUT /fornecedores/{id}/avaliacao` |
| Clientes | CRUD em `/clientes` · `GET /clientes/{id}/orcamentos` |
| Importação | `POST /importacoes/arquivo` (multipart) · `POST /importacoes/texto` · `GET /importacoes/{id}` · `PUT /importacoes/{id}/itens/{itemId}` · `POST /importacoes/{id}/itens` · `DELETE /importacoes/{id}/itens/{itemId}` · `POST /importacoes/{id}/confirmar-revisao` |
| Orçamentação ✅ | `POST /orcamentos/gerar` (lista revisada; `modo` Unico/Comparativo; `opcoes`) · `GET /orcamentos/{id}/comparacao` · `POST /orcamentos/{id}/opcoes/{opcaoId}/selecionar` · `PUT /orcamentos/{id}/opcoes/{opcaoId}/itens/{itemId}/preco` · `POST /orcamentos/{id}/status` |
| Empresa ✅ | `GET /empresa/configuracoes` · `PUT /empresa/configuracoes` (Admin: modo padrão de orçamento) |
| Orçamentos | `GET /orcamentos?status=&clienteId=&de=&ate=` · `GET /orcamentos/{id}` · `POST` · `PUT /{id}` · `DELETE /{id}` · `POST /{id}/duplicar` · `PATCH /{id}/status` · `PUT /{id}/opcoes/{opcaoId}/itens/{itemId}` · `GET /{id}/pdf` |
| Estoque ✅ | `GET /estoque?abaixoDoMinimo=&busca=` · `GET /estoque/{produtoId}` · `POST /estoque/{produtoId}/entradas` · `POST /estoque/{produtoId}/saidas` · `POST /estoque/{produtoId}/ajustes` (Gerente) · `PUT /estoque/{produtoId}/configuracao` (Gerente) · `GET /estoque/{produtoId}/movimentacoes` · baixa automática ao finalizar o orçamento |
| Relatórios | `GET /relatorios/{orcamentos,produtos-mais-pesquisados,produtos-mais-comprados,evolucao-precos,fornecedores-mais-utilizados,economia,movimentacoes-estoque,compras}?de=&ate=&produtoId=&fornecedorId=&clienteId=` |
| Integrações | `GET /integracoes/provedores` (status de cada provedor) · `POST /cotacoes/pesquisar` |
| Sistema | `GET /health` · `/swagger` |

Respostas de erro seguem **RFC 7807 (ProblemDetails)**. Listagens são paginadas (`pagina`, `tamanhoPagina`, máx. 100).

## 6. Permissões

| Ação | Operador | Gerente | Administrador |
|---|---|---|---|
| Cadastros (CRUD, exceto excluir) | ✔ | ✔ | ✔ |
| Excluir/desativar cadastros | | ✔ | ✔ |
| Importar, gerar e editar orçamentos | ✔ | ✔ | ✔ |
| Aprovar/reprovar e excluir orçamentos | | ✔ | ✔ |
| Movimentar estoque | ✔ | ✔ | ✔ |
| Ajuste de estoque, relatórios financeiros | | ✔ | ✔ |
| Usuários, integrações e configurações | | | ✔ |

Implementado com *policies* do ASP.NET Core (`[Authorize(Policy = "Gerente")]`).

## 7. Fluxo completo de funcionamento

```
1. LOGIN ─► JWT (15 min) + refresh token (7 dias)

2. IMPORTAÇÃO
   arquivo/texto ─► validação (extensão + assinatura "magic bytes" + tamanho ≤ 10 MB)
     ├─ PDF com texto ─► PdfPig
     ├─ PDF escaneado / imagem ─► OCR (Tesseract)
     ├─ Excel ─► ClosedXML (detecção de colunas: descrição, qtd, unidade, marca)
     ├─ Word ─► OpenXml (parágrafos e tabelas)
     └─ Texto / WhatsApp ─► limpeza (remove "[10:32, 14/11] Fulano:", emojis, marcadores)
   ─► Interpretador de linhas:
        "20 cx caneta esferográfica azul BIC" → Qtd 20 | Un CX | Desc "caneta esferográfica azul" | Marca BIC
        (regras regex + dicionário de unidades e sinônimos + marcas cadastradas)
   ─► Associação com o catálogo (busca por similaridade pg_trgm) + nível de confiança
   ─► Status = AguardandoRevisao

3. REVISÃO (obrigatória)
   usuário corrige, exclui e adiciona itens ─► "Confirmar revisão" ─► Status = Revisada
   (o domínio recusa gerar orçamento de lista não revisada)

4. COTAÇÃO
   para cada item: consulta em paralelo, com timeout, todos os IProvedorPrecos ativos
     + preços de ProdutoFornecedor + último HistoricoPreco
   ─► normalização ─► grava Cotacao (com Origem)
   provedor fora do ar ─► registra, segue com os demais e sinaliza na tela

5. MOTOR DE ORÇAMENTAÇÃO (Strategy)
   ├─ CustoBenefício: score = preço normalizado + reconhecimento da marca + avaliação do fornecedor + disponibilidade
   ├─ MarcasReconhecidas: filtra marcas com nível ≥ 4 e fornecedores com avaliação ≥ 4, depois o menor preço
   └─ MenorPreco: menor preço entre ofertas disponíveis que atendem a especificação (mesma unidade/medida)
   item sem nenhuma oferta ─► "Sem cotação": preço manual obrigatório, nunca inventado
   ─► Orcamento + até 3 OpcaoOrcamento + ItemOrcamento, totais calculados no domínio

6. COMPARAÇÃO ─► totais, diferenças em R$ e %, melhor preço por item, marcas, fornecedores, disponibilidade
   ─► usuário seleciona a opção

7. GESTÃO ─► EmElaboracao → AguardandoAprovacao → Aprovado/Reprovado → Finalizado
   ─► PDF da proposta (opção selecionada)

8. COMPRA / ESTOQUE ─► ao finalizar: "Dar entrada no estoque" gera MovimentacaoEstoque vinculada
   ─► saldo atualizado ─► alerta se abaixo do mínimo

9. DASHBOARD E RELATÓRIOS ─► consultas agregadas sobre os dados acima
   (economia = total da opção mais cara − total da opção selecionada)
```

### Transições de status permitidas

| De | Para |
|---|---|
| EmElaboracao | AguardandoAprovacao |
| AguardandoAprovacao | Aprovado, Reprovado, EmElaboracao |
| Aprovado | Finalizado |
| Reprovado | EmElaboracao |
| Finalizado | — (terminal) |

Orçamentos só podem ser editados em `EmElaboracao`.

## 8. Integração de preços — honestidade dos dados

Não existe API pública, gratuita e universal de preços de materiais no Brasil. Por isso:

1. `IProvedorPrecos` define `PesquisarAsync(termo, unidade, ct)` e retorna ofertas normalizadas.
2. Cada provedor tem configuração própria (`appsettings: Integracoes:Precos:{Nome}`: habilitado, URL, chave via variável de ambiente, timeout), política de retry e *circuit breaker* (Microsoft.Extensions.Http.Resilience) e logs.
3. **`ProvedorPrecosSimulado`** vem habilitado só em `Development`. Todas as ofertas dele saem com `Origem = Simulado`, e a UI e o PDF mostram o aviso "Preço simulado — não usar para compra".
4. Fontes reais que podem ser conectadas depois: APIs de fornecedores parceiros, marketplaces com API (ex.: Mercado Livre), planilhas de preço enviadas pelos fornecedores (importação para `ProdutoFornecedor`) e cotação manual.

## 9. Segurança

Hash PBKDF2 · JWT com chave de no mínimo 256 bits lida de variável de ambiente (`ORCAFACIL_JWT__CHAVE`) · refresh token salvo com hash · EF Core parametrizado (sem SQL concatenado) · FluentValidation em toda entrada · middleware global que devolve ProblemDetails sem stack trace em produção · rate limiting no login · CORS restrito à origem do frontend · upload validado por extensão, MIME **e** assinatura, limite de 10 MB, salvo fora da pasta web com nome aleatório · logs sem dados sensíveis (senha, token, documento mascarado) · `.env` e `appsettings.*.local.json` no `.gitignore`.

## 10. Plano de fases

| Fase | Entrega | Estado |
|---|---|---|
| 1 | Planejamento, arquitetura, esqueleto da solução, entidades e regras de domínio | ✅ |
| 2 | DbContext, configurações EF, índices, migration inicial, seed | ✅ |
| 2.1 | Multiempresa (SaaS) e neutralização do segmento | ✅ |
| 3 | Infra de API: middleware de erros, ProblemDetails, Serilog, paginação, repositórios | ✅ |
| 4 | Autenticação e usuários | ✅ |
| 5 | Produtos, categorias, marcas, fornecedores, clientes | ✅ |
| 6 | Importação de listas (texto, WhatsApp, Excel, Word, PDF, imagem/OCR) e revisão | ✅ |
| 7 | Motor de orçamentação (preço de venda, Único/Comparativo, produtos equivalentes) e comparação | ✅ |
| 8 | Estoque e disponibilidade no orçamento | ✅ |
| 9 | PDF | |
| 10–11 | Frontend e integração | |
| 12 | Testes de integração (Testcontainers) e correções | |
| 13 | Documentação final, Dockerfiles, CI | |
