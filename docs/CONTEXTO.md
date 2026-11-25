# OrçaFácil — Contexto para continuar o desenvolvimento

> **Leia este arquivo primeiro ao retomar o projeto.** Ele é atualizado ao fim de cada sessão.
> Para continuar com o Claude, conecte a pasta (ou envie este arquivo) e diga: "continue o OrçaFácil de onde paramos".

## Onde o projeto está salvo

- Pasta local: `C:\Users\Compu\OneDrive\Área de Trabalho\OrcaFacil`
- Ambiente da usuária: Windows, .NET SDK 10.0.302, VS Code, PowerShell.
- Os comandos `dotnet` são executados dentro de `OrcaFacil\backend` (não na raiz).
- Atenção: o disco C: ficou sem espaço na primeira restauração do NuGet. Se der erro "Espaço insuficiente", liberar espaço antes.

## ▶️ ONDE PARAMOS (25/11/2026): retomar por aqui

**Fases 7 e 8 concluídas e validadas.** Falta só a usuária fazer o **commit "Fase 8" + push** (Source Control → Commit & Sync; conferir com `git log` no computador).

**Próximo passo: Fase 9, orçamento em PDF + texto pronto para o WhatsApp** (visão do produto, item 6). Lembrar: QuestPDF é gratuito só até US$ 1 mi de receita anual (avisar a usuária antes de adotar; alternativa sem licença comercial: PdfSharp/MigraDoc ou gerar HTML). Propor o plano antes de codar.

**Como testar no Swagger agora (desde 25/11):** a usuária pediu que Claude teste sozinho. Claude usa o **navegador embutido do app** (`mcp__remote-devices__Claude_Browser__*`, site `https://localhost:7080` já liberado). Ela precisa: build/test/run no Windows; abrir o painel (Ctrl+Shift+B no app desktop) e fazer o Authorize LÁ (não no Chrome dela). Claude pega o token em `window.ui.authSelectors.authorized()` e chama a API com `fetch` via `javascript_tool`. Nunca digitar a senha dela.

- **GitHub:** https://github.com/Alinefbertolli/OrcaFacil (público). Commit + push ao fim de cada etapa validada. VS Code em inglês e logado no GitHub; a usuária usa o painel Source Control (Commit → Sync Changes).
- **Senha do Postgres** foi trocada em 23/11 por uma aleatória (está no `.env` e nos user-secrets). Nunca pedir.
- **Dados de teste no banco:** marcas Tilibra (5) e Foroni (3); produtos CAD10-1/2/3 (12/20/30, grupo "caderno 10 materias"); lista `01a20b03-79ec-7be1-9733-47d5e3aba86a` (Revisada); ORC-2026-000001 (Comparativo, Econômica selecionada, AguardandoAprovacao) e ORC-2026-000002 (Unico).
- **Lições operacionais para Claude:** (1) nunca chamar `device_commit_files` em paralelo com o Bash que prepara o arquivo; (2) **usar um caminho de staging NOVO a cada envio** (ex.: `/mnt/user-data/outputs/envio-AAAAMMDD-x/...`): reenviar pelo mesmo stagedPath gravou a versão antiga; (3) depois de enviar, conferir no computador com `grep` via `device_bash`; (4) o Windows PowerShell 5.1 não tem `RandomNumberGenerator.Fill` (usar `Create().GetBytes()`); (5) Swagger guarda o token no navegador (EnablePersistAuthorization): 401 com "token expired" = fazer Logout + Authorize de novo.

## 🎯 Visão do produto (decidida em 18/11/2026; orienta todas as fases seguintes)

O OrçaFácil é usado pela **LOJA para responder ao CLIENTE FINAL**. Exemplos da usuária:
- a mãe envia à papelaria a foto da lista de material escolar e recebe o orçamento;
- o pedreiro envia à loja de construção a lista de material e recebe o valor;
- a cliente de uma loja de pijamas/roupas que vende por catálogo no WhatsApp envia itens exatos ("pijama Mayara", "pijama Luna").

Consequências:
1. **O preço do orçamento é o PREÇO DE VENDA da loja** (campo novo `Produto.PrecoVenda`, substituindo o "preço de referência"). Os preços dos fornecedores (`ProdutoFornecedor`) são o CUSTO: uso interno (reposição, margem), não aparecem para o cliente.
2. **As 3 opções comparam produtos EQUIVALENTES do catálogo da própria loja** (marcas e preços diferentes), não fornecedores: Econômica (menor preço), Custo-benefício (equilíbrio preço x reconhecimento da marca) e Marcas reconhecidas (nível de marca 4–5).
3. **Modo escolhido na hora + padrão por empresa:** "Orçamento único" (itens exatos do catálogo, ex.: loja de pijamas) ou "Comparativo" (escolher quais das 3 opções gerar). Se as opções saírem idênticas, mostrar uma só, com aviso.
4. **Marca pedida pelo cliente é respeitada:** se a linha diz "caderno Tilibra", todas as opções usam Tilibra (se houver no catálogo).
5. Item sem produto ou sem preço de venda → "sem preço" destacado; nunca inventar valor.
6. Futuro: disponibilidade pelo estoque (Fase 8); orçamento em PDF e em texto pronto para o WhatsApp (Fase 9).

## Perfil da usuária

Estudante do 1º semestre de ADS e primeiro projeto com IA. **Explicar cada etapa de forma didática** (o quê, por quê, como; comandos explicados). Material de estudo: `docs/GUIA-DO-ESTUDANTE.md`, que ganha uma seção nova a cada fase.

## Situação atual

| Fase | Descrição | Estado |
|---|---|---|
| 1 | Planejamento, arquitetura, esqueleto da solução, domínio e testes de domínio | ✅ **Validada** (build e testes OK em 15/11/2026) |
| 2 | DbContext, configurações EF Core, índices, sequência, seed de categorias, DI, health check do banco | ✅ **Concluída (16/11/2026)**: 40 testes OK, migration `Inicial` aplicada no PostgreSQL (Docker), `/health` = Healthy |
| 2.1 | **Multiempresa (SaaS) + neutralização do segmento** | ✅ **Concluída (17/11/2026)**: 62 testes OK, migration `Inicial` recriada (`20261117184755_Inicial.cs`), `/health` Healthy |
| 3 | Infra da API: middleware de exceções/ProblemDetails, Serilog, paginação, repositórios, UnitOfWork | ✅ **Concluída (17/11/2026)**: 80 testes OK, build sem avisos, Swagger testado |
| 4 | Autenticação (JWT + refresh token), cadastro de empresa + usuário, permissões | ✅ **Concluída (17/11/2026)**: testes OK; no Swagger: registrar 201, /me 200, sem token 401, login 200/401, rate limit 429 |
| 5 | Produtos, categorias, marcas, fornecedores, clientes | ✅ **Concluída (18/11/2026)**: 149 testes OK; roteiro completo no Swagger (categoria, marca, produto, 409 de código repetido, fornecedor, vínculo com preço, histórico, busca, cliente com CPF inválido → 400) |
| 6 | Importação de listas (PDF, imagem/OCR, Excel, Word, texto, WhatsApp) e revisão | ✅ **Concluída (18/11/2026)**: 195 testes OK; no Swagger: WhatsApp (3 itens, caderno sugerido do catálogo), correção, sugestões, confirmação (depois 422 ao editar), Excel (2 itens), imagem com OCR (3 itens, confiança do Tesseract 88%) |
| 7 | Motor de orçamentação (preço de venda, modo Único/Comparativo, 3 opções com produtos equivalentes) e comparação | ✅ **Concluída (25/11/2026)**: 228 testes OK; migration incremental `Fase7MotorOrcamento` (rename sem perda de dados); no Swagger: comparativo 24/40/60, comparação 0/16/36 (0/66,7/150 %), seleção, 422 com item sem preço, preço manual 31,50, envio 200, modo Único 201 |
| 8 | Estoque e disponibilidade no orçamento | ✅ **Concluída (25/11/2026)**: 246 testes OK; sem migration; testada por Claude no navegador embutido: entradas/saídas/ajuste/mínimo/histórico, 422 sem controle e saldo insuficiente, comparativo preferindo quem tem estoque (Econômica = Foroni), finalização bloqueada sem saldo e baixa de 2 ligada ao ORC-2026-000004 |
| 9 | PDF do orçamento e texto para WhatsApp | ⏭️ Próxima |
| 8 | Estoque (endpoints) | Pendente |
| 9 | Geração de PDF (QuestPDF) | Pendente |
| 10–11 | Frontend React + TS + MUI e integração | Pendente |
| 12 | Testes de integração (Testcontainers) e correções | Pendente |
| 13 | Documentação final, Dockerfiles, CI | Pendente |

### Registro da validação da Fase 2 (concluída)

1. ✅ `dotnet build` (1 aviso, ainda não identificado) e `dotnet test` (40/40).
2. Instalar o **Docker Desktop** (WSL2) — escolha da usuária para aprender Docker. A máquina não tinha Docker em 15/11/2026. Depois: `copy .env.example .env`, definir `POSTGRES_PASSWORD` e rodar `docker compose up -d` na raiz do projeto. O contêiner já cria o usuário e o banco `orcafacil`. Atenção ao espaço livre no disco C: (recomendado ~10 GB).
3. Configurar a connection string com user-secrets e gerar a migration `Inicial` (passo a passo no README, seção "Banco de dados").
4. `dotnet run --project OrcaFacil.API` → `https://localhost:7080/health` deve responder `Healthy`.

Andamento em 16/11/2026: Docker Desktop instalado e funcionando; `.env` e user-secrets configurados. O `dotnet ef` falhou ao não achar a connection string porque o user-secrets só carrega em Development. A primeira hipótese (ambiente diferente de Development) não resolveu. **Causa real:** `OrcaFacil.API/obj/Debug/net10.0/OrcaFacil.API.AssemblyInfo.cs` estava com 0 bytes, corrompido quando o disco encheu no primeiro build. O build incremental o considerava atualizado e não o regenerava, então o atributo `UserSecretsId` não entrava na DLL e o user-secrets nunca era lido. **Solução:** apagar as pastas `bin` e `obj` e compilar de novo. Lição: depois de "disco cheio", sempre limpar `bin`/`obj`.

Pontos de maior risco na Fase 2, caso algum teste falhe:
- `configurationBuilder.Properties<Enum>().HaveConversion<string>()` (teste `Enums_devem_ser_gravados_como_texto`). Se falhar, configurar `HaveConversion<string>()` por propriedade.
- `Endereco` (record) como owned type.
- `Estoque.Versao` → `IsRowVersion()` mapeado para `xmin` pelo Npgsql.

## Decisões tomadas (não rediscutir sem motivo)

1. **.NET 10**, solução `.slnx`, configurações comuns em `backend/Directory.Build.props` (nullable ativado; avisos de nulabilidade tratados como erro).
2. **Domínio rico** para Orçamento, Estoque e Importação. Cadastros simples (Cliente, Categoria, Fornecedor) são validados na Application com FluentValidation.
3. **Entidades além do briefing:** `OpcaoOrcamento` (até 3 opções por orçamento), `Marca` (nível de reconhecimento 1–5 definido pelo usuário), `RefreshToken`, `TokenRecuperacaoSenha`.
4. **Todo preço tem `OrigemPreco`** (Integracao, FornecedorCadastrado, Manual, Estimado, Simulado). O provedor simulado só funciona em Development e sempre marca o preço como `Simulado`.
5. **Revisão obrigatória:** `ListaImportada.GarantirProntaParaOrcamento()` bloqueia a geração de orçamento se a lista não estiver `Revisada`.
6. **Status do orçamento:** EmElaboracao → AguardandoAprovacao → Aprovado/Reprovado → Finalizado (terminal). Só é possível editar em EmElaboracao, e o envio para aprovação exige uma opção selecionada.
7. **Economia** = opção mais cara − opção selecionada.
8. **Estoque:** saldo nunca negativo (no domínio e por CHECK no banco); toda alteração gera uma `MovimentacaoEstoque` imutável; concorrência otimista via `xmin`.
9. **IDs** `Guid` v7 gerados no domínio, com `ValueGeneratedNever` aplicado a todas as entidades no DbContext (sem isso, filhos novos de agregados carregados virariam UPDATE).
10. **Datas** em UTC (`timestamptz`), preenchidas por `OrcaFacilDbContext.PreencherDatasDeAuditoria`.
11. **Persistência:** enums como texto (varchar 40); dinheiro `numeric(18,2)`, preço unitário `numeric(18,4)`, quantidade `numeric(18,3)`; nomes de tabela no plural (`Orcamentos`, `OpcoesOrcamento`…); `Endereco` em colunas `Endereco*`; `LogSistema.DetalhesJson` → coluna `Detalhes jsonb`.
12. **Número do orçamento:** por empresa e por ano, `ORC-AAAA-NNNNNN`, via tabela `ContadoresNumeracao` (substituiu a sequência global em 17/11/2026).
13. **Seed:** nenhum dado pré-cadastrado (o sistema é neutro quanto ao segmento). A empresa e o primeiro usuário são criados pelo cadastro (Fase 4).
14. **Licenças:** testes com **AwesomeAssertions** (Apache 2.0, no lugar do FluentAssertions desde 15/11/2026). QuestPDF é gratuito até US$ 1 mi de receita anual.
15. **Segredos** nunca no código: user-secrets (`UserSecretsId = orcafacil-api`) ou variáveis `ORCAFACIL_*`; a senha do Postgres fica no `.env`. A API não sobe sem a connection string (mensagem de erro explica como configurar).
16. **SaaS multiempresa:** isolamento por `EmpresaId` com filtro global do EF Core; falha "fechada" (sem empresa identificada, nada é lido nem gravado).
17. **Autenticação:** JWT de 15 min + refresh token rotativo de 7 dias (só o hash no banco; o reuso de token revogado encerra todas as sessões). Todo endpoint exige login, exceto os marcados com [AllowAnonymous]. Perfis: Operador < Gerente < Administrador (políticas `Gerente` e `Administrador`). O e-mail do usuário é único no sistema todo.

## Rotina para subir o ambiente (todo dia)

1. Abrir o Docker Desktop e esperar "Engine running".
2. Na raiz `OrcaFacil`: `docker compose up -d` e `docker ps` (healthy).
3. Em `OrcaFacil\backend`: `dotnet run --project OrcaFacil.API` → https://localhost:7080/swagger

## Mapa dos arquivos principais

- Domínio: `backend/OrcaFacil.Domain/Entidades/*.cs`, `Enums/Enumeracoes.cs`, `ObjetosDeValor/`
- Persistência: `backend/OrcaFacil.Infrastructure/Persistencia/OrcaFacilDbContext.cs`, `Configuracoes/*Configuracoes.cs`, `Migrations/` (gerada pela usuária; `Estoque.Versao` = coluna de sistema `xmin`)
- DI: `backend/OrcaFacil.Infrastructure/DependencyInjection.cs` → `AddInfraestrutura()`
- Interfaces da Application: `backend/OrcaFacil.Application/Abstracoes/`
- Testes: `backend/OrcaFacil.Tests/Domain/`, `backend/OrcaFacil.Tests/Infrastructure/`

## ✅ Fase 2.1: o que foi implementado (17/11/2026)

- **Domínio:** `Empresa` (CNPJ validado, `SegmentoEmpresa`, Ativa); base `EntidadeDaEmpresa` (`Comum/EntidadeDaEmpresa.cs`) com `EmpresaId` imutável (`VincularEmpresa`) e `PropagarEmpresaPara` (usada em `Orcamento.Duplicar`, `Estoque.Movimentar` e `ProdutoFornecedor.AtualizarPreco`). As 13 entidades de dados herdam dela. `LogSistema.EmpresaId` é opcional e não filtrado. Tokens e filhos de agregado não têm `EmpresaId`.
- **Application:** `IEmpresaAtual { Guid? EmpresaId }`.
- **API:** `Seguranca/EmpresaAtualPorToken.cs` lê o claim `empresa_id` do JWT (constante `ClaimEmpresaId`). Sem login, devolve null.
- **Infrastructure:** o `OrcaFacilDbContext` recebe `IEmpresaAtual`; filtro global `EmpresaId == EmpresaIdAtual` + FK para `Empresas` em toda `EntidadeDaEmpresa` (método genérico chamado por reflexão); `AplicarRegrasAntesDeSalvar()` (internal, testável) preenche o `EmpresaId` dos novos registros, bloqueia gravação em outra empresa e a troca de empresa. Índices únicos compostos com `EmpresaId`. Seed de categorias removido. Sequência global trocada por `ContadoresNumeracao` (`Numeracao.cs` + `GeradorNumeroOrcamento.cs`, número por empresa e por ano).
- **Testes:** exemplos trocados para papelaria; novos `EmpresaTests`, `EntidadeDaEmpresaTests`, `IsolamentoEmpresasTests` (via `ToQueryString` e `AplicarRegrasAntesDeSalvar`, sem banco) e índices compostos em `ModeloBancoTests`.
- **Migration:** como não havia dados reais, a decisão foi **recriar a `Inicial`** (apagar a pasta `Migrations`, `database drop`, `migrations add Inicial`, `database update`) em vez de criar uma migration incremental. A partir de agora (com dados reais), sempre criar migrations incrementais.

**Para a Fase 4 (autenticação), lembrar:**
- O login busca o usuário por e-mail **antes** de haver empresa: usar `IgnoreQueryFilters()` nessa consulta.
- `POST /auth/registrar` cria a `Empresa` + o primeiro usuário (Administrador), chamando `usuario.VincularEmpresa(empresa.Id)` explicitamente.
- Gravar o claim `empresa_id` no JWT.
- Futuro: "pacotes de categorias por segmento" oferecidos no cadastro da empresa.

## Roteiro original da Fase 2.1 (decidido em 16/11/2026)

**Motivo:** a usuária quer oferecer o sistema a varejistas de vários segmentos (ex.: papelaria), por isso o sistema não pode ser focado em material de construção. **Decisão: opção B, SaaS multiempresa** (um sistema, um banco, dados isolados por `EmpresaId`). A opção A (uma instalação por cliente) foi descartada.

1. **Entidade `Empresa`:** RazaoSocial, NomeFantasia, Cnpj (único), Segmento (texto livre ou enum: Construcao, Papelaria, Informatica, Outro), Ativa.
2. **Interface `IEntidadeDaEmpresa { Guid EmpresaId }`** nas entidades de dados do cliente: Usuario, Cliente, Categoria, Marca, Produto, Fornecedor, ProdutoFornecedor, HistoricoPreco, Cotacao, ListaImportada, Orcamento, Estoque, MovimentacaoEstoque, LogSistema. Os filhos de agregado (ItemListaImportada, OpcaoOrcamento, ItemOrcamento) herdam a empresa pela raiz e não precisam da coluna.
3. **Unicidades passam a valer por empresa:** (EmpresaId, Codigo) em Produto, (EmpresaId, Nome) em Categoria e Marca, (EmpresaId, Cnpj) em Fornecedor, (EmpresaId, Documento) em Cliente, (EmpresaId, Numero) em Orcamento. O e-mail do usuário continua único no sistema todo (login global).
4. **Filtro global do EF Core** (`HasQueryFilter(e => e.EmpresaId == _empresaAtual.Id)`), com `IEmpresaAtual` (Application) preenchido a partir do JWT na Fase 4. Preencher o `EmpresaId` automaticamente no `SaveChanges` para entidades novas.
5. **Número do orçamento por empresa:** a sequência global não serve; usar uma tabela de contadores por empresa ou aceitar a sequência global (avaliar).
6. **Neutralizar o segmento:** remover o seed das 8 categorias de construção; trocar os exemplos de construção nos testes e docs por exemplos neutros/variados; no futuro, oferecer "pacotes de categorias por segmento" no cadastro da empresa.
7. Testes de domínio e de modelo (o filtro existe; os índices únicos são compostos com EmpresaId).
8. Nova migration (sugestão de nome: `Multiempresa`), gerada e aplicada pela usuária (lembrar de explicar o `migrations add` + `database update`).
9. Atualizar ARQUITETURA.md (modelo de dados) e GUIA-DO-ESTUDANTE.md (explicar multi-tenancy com analogia: prédio de escritórios, cada empresa com sua chave).

## ✅ Fase 8: o que foi implementado (25/11/2026)

- **Decisões da usuária:** no Comparativo, preferir produtos que atendem a quantidade (saldo suficiente OU sem controle de estoque); se nenhum atende, regra normal + observação "Nenhum produto equivalente tem estoque suficiente". Baixa automática ao **finalizar** (Aprovado → Finalizado), no mesmo SaveChanges; se faltar saldo de qualquer item, nada é baixado e a finalização é recusada (422) com a lista do que falta.
- **Produto "não controlado"** = sem registro de Estoque; o controle começa na 1ª entrada, ajuste ou configuração (substitui a ideia de criar o Estoque ao cadastrar o produto).
- **Domínio:** `Estoque.RegistrarSaida(..., orcamentoId)`; `TamanhoMaximoLocalizacao`.
- **Application/Estoques:** `DisponibilidadeEstoque` (NaoControlado, Disponivel, Parcial, Indisponivel) + `Disponibilidade.Avaliar/Atende`; `IRepositorioEstoques`; `ServicoEstoque` (obter, listar com abaixoDoMinimo/busca, entrada, saída, ajuste com motivo obrigatório, configurar mínimo/localização, movimentações). Orçamentos: `ProdutoCandidato.SaldoEstoque`, `SeletorDeOpcoes.FiltrarPorEstoque`, itens e células da comparação com `Disponibilidade` e `SaldoEmEstoque` calculados NA HORA da consulta, aviso "sem estoque suficiente no momento" (exceto em Finalizado).
- **Infrastructure:** `RepositorioEstoques`; `CatalogoEquivalentes` projeta o saldo (`p.Estoque`).
- **API:** `EstoqueController` em `/api/v1/estoque` (ajustes e configuração exigem Gerente).
- Sem migration (as tabelas Estoques/MovimentacoesEstoque existem desde a Fase 2).

## ✅ Fase 7: o que foi implementado (23–25/11/2026)

- **Domínio:** `Produto.PrecoVenda` (substitui PrecoReferencia/OrigemPrecoReferencia) e `Produto.GrupoEquivalencia` (normalizado: minúsculas, sem acento, espaços simples; máx. 100); `Empresa.ModoOrcamentoPadrao`; enum `ModoOrcamento` (Unico, Comparativo); `TipoOpcaoOrcamento.Unica`; `OrigemPreco.PrecoVenda`; `Orcamento.Modo`, `LimparSelecao()`, `InformarPrecoItem()`; envio para aprovação bloqueado se a opção selecionada tiver itens sem preço; `ItemOrcamento.SemPreco(...)` (`PrecoPendente`, `Observacao` máx. 500, `ItemListaImportadaId`, `Ordem`); `Duplicar` preserva tudo isso.
- **Application/Orcamentos:** `SeletorDeOpcoes` (regras puras: Econômica = menor preço; Custo-benefício = mais barato entre marcas de nível ≥ 3, senão o mais barato; Marcas reconhecidas = maior nível, empate → mais barato; produto sem marca = nível 1; nunca escolhe produto sem preço; filtro da marca pedida sem acento/maiúsculas, por nome da marca ou do produto); `ServicoOrcamentos` (gerar, listar, obter, comparar, informar preço, selecionar em 2 gravações por causa do índice único de opção selecionada, alterar status; opções idênticas removidas com aviso; opção única já selecionada; aviso de unidade diferente; validade padrão 7 dias). `Empresas/ServicoConfiguracoesEmpresa`.
- **Infrastructure:** `CatalogoEquivalentes` (grupo → mesmo grupo; sem grupo → mesma categoria com `similarity(Nome) >= 0.5`; só ativos), `RepositorioOrcamentos` (Include + ThenInclude + AsSplitQuery). Índices `(EmpresaId, GrupoEquivalencia)` e `(OpcaoOrcamentoId, Ordem)`.
- **API:** `OrcamentosController` (`POST /orcamentos/gerar`, `GET /orcamentos`, `GET /orcamentos/{id}`, `GET /{id}/comparacao`, `PUT /{id}/opcoes/{opcaoId}/itens/{itemId}/preco`, `POST /{id}/opcoes/{opcaoId}/selecionar`, `POST /{id}/status`); `EmpresaController` (`GET /empresa/configuracoes`, `PUT` só Administrador).
- **Migration** `20261123221525_Fase7MotorOrcamento`: o EF gerou RenameColumn sozinho; Claude ajustou `defaultValue: "Comparativo"` nas colunas novas de enum.
- **Ainda não feito (previsto):** trocar o produto de um item do orçamento manualmente; duplicar pela API; `GET /clientes/{id}/orcamentos`; PDF/WhatsApp (Fase 9); renovação automática do token no frontend (Fase 10).

## ✅ Fase 6: o que foi implementado (18/11/2026)

- **Domínio:** `ItemListaImportada.SugerirProduto(produtoId, novaConfianca)`; `Corrigir` agora aceita `produtoId = null` (desfaz a associação).
- **Application/Importacao/:** `UnidadesDeMedida` (dicionário de sinônimos → UN, CX, PCT, KG, G, M, M2, M3, L, ML, RESMA, RL, SC, DZ, PAR, KIT, FD, GL, LATA, TB, BL, CJ, FR); `InterpretadorLista` (estático; regex para os prefixos do WhatsApp copiado e exportado, marcadores, frases de conversa, emojis; quantidade no início [inclusive grudada na unidade, "20cx"] ou no fim [com separador - : ( x, ou seguida de unidade conhecida]; número seguido de palavra que não é unidade NÃO é quantidade; "1.000" = mil; marca explícita "marca X" ou marca cadastrada como palavra inteira; confiança = 0,4 +0,3 qtd +0,1 unidade +0,05 marca, máx. 0,95; ConfiancaParaDispensarAtencao = 0,7; máx. 500 itens; regras simuladas em Node antes da entrega); `ValidadorArquivo` (≤ 10 MB; .txt .pdf .xlsx .docx .png .jpg .jpeg .bmp .tif .tiff; .xls/.doc com orientação "Salvar como"; magic bytes); `Abstracoes.cs` (IExtratorTexto, IServicoOcr, IArmazenamentoArquivos, IBuscaProdutosSimilares + ProdutoSimilar, IRepositorioListasImportadas, ArquivoEnviado); `ServicoImportacao` (importar texto/arquivo, listar, obter, atualizar/adicionar/excluir item, sugestões, confirmar revisão; sugere produto se similaridade ≥ 0,5 e soma +0,15 de confiança; arquivo ilegível → lista salva com status Erro + DomainException 422).
- **Infrastructure/Importacao/:** `ExtratorTexto` (txt; Excel com detecção de cabeçalho Produto/Descrição/Item/Material, Qtd/Quant, Un/Medida, Marca → "qtd un descrição marca: X"; Word com parágrafos e linhas de tabela; PDF via PdfPig ContentOrderTextExtractor e, com menos de 20 letras, OCR das imagens das páginas; erros das bibliotecas → DomainException "corrompido"); `ServicoOcrTesseract` (pacote `Tesseract 5.*`, `Ocr:CaminhoTessdata` = "tessdata", `Ocr:Idioma` = "por"; sem o arquivo de idioma → DomainException com instrução; DllNotFound → orienta instalar o VC++ Redistributable); `ArmazenamentoLocal` (`Uploads:Diretorio`, {empresa}/{ano}/{mês}/{guid}.ext); `BuscaProdutosSimilares` (SQL com GREATEST(similarity, word_similarity), operadores % e <%, filtro explícito de EmpresaId e Ativo). `Persistencia/Repositorios/RepositorioListasImportadas` (Include dos itens).
- **Pacotes:** ClosedXML 0.*, DocumentFormat.OpenXml 3.*, PdfPig 0.*, Tesseract 5.* (também referenciado na API para copiar as DLLs nativas).
- **API:** `ImportacoesController` (texto, arquivo multipart com RequestSizeLimit de 12 MB, listar, obter, revisão de itens, sugestões, confirmar). `.gitignore` inclui `tessdata/`.
- **Testes:** InterpretadorListaTests, ValidadorArquivoTests, ExtratorTextoTests (Excel, Word e PDF reais gerados em memória; OCR fake; OCR sem idioma), ServicoImportacaoTests.
- **Não precisa de migration.**

## ✅ Fase 5: o que foi implementado (17/11/2026)

- **Repositório genérico** ganhou `ObterAsync(condição)`, `ListarTodosAsync(filtro)` (sem paginação; só para conjuntos pequenos) e `ListarPorIdsAsync(ids)` (evita N+1). Implementados no EF e no `FakeRepositorio`.
- **Application/Comum/Validacao.cs:** `Validacao.Erro(campo, msg)` (ValidationException → 400), `NormalizarBusca`, `TextoOpcional`.
- **Application/Cadastros/:** `Endereco.cs` (EnderecoValidador + EnderecoPadronizado: trim, UF maiúscula, CEP só dígitos, todo em branco → null; o DTO usa o próprio record de domínio `Endereco`), `Categorias.cs` (subcategorias com validação de ciclo; exclusão física só sem produtos e sem subcategorias), `Marcas.cs` (nível 1–5; exclusão física só sem produtos), `Produtos.cs` (código único por empresa sem diferenciar maiúsculas; unidade em maiúsculas; preço de referência sempre com origem Manual + HistoricoPreco; filtros por busca, categoria, marca e incluirInativos; vínculo com fornecedor por upsert com histórico só quando o preço muda; remover vínculo mantém o histórico; listagem do histórico), `Fornecedores.cs` (CNPJ só dígitos e único por empresa; avaliação 0–5; e-mail minúsculo; produtos do fornecedor), `Clientes.cs` (CPF/CNPJ opcional, validado conforme o TipoPessoa, único por empresa).
- **Exclusão:** física para categorias e marcas; lógica (Ativo=false + reativar) para produtos, fornecedores e clientes. Excluir, desativar, reativar e remover vínculo exigem a política **Gerente**.
- **API:** `CategoriasController`, `MarcasController`, `ProdutosController` (+ `PUT/DELETE {id}/fornecedores/{fornecedorId}`, `GET {id}/historico-precos`, `POST {id}/reativar`), `FornecedoresController` (+ `GET {id}/produtos`), `ClientesController`. POST → 201 com `CreatedAtAction`.
- **Testes:** `Tests/Application/CadastrosTests.cs` (categorias, marcas, produtos, fornecedores e clientes).
- **Não precisa de migration** (as tabelas já existiam).
- **Correções durante a validação (17/11/2026):** (1) o `UseSerilogRequestLogging` passou para ANTES do `UseExceptionHandler` (antes, erros tratados como 404/409 apareciam no log como `[ERR] responded 500`); (2) `appsettings.Development.json` registra o motivo de falha do JWT (`Microsoft.AspNetCore.Authentication: Information`). O 401 do Passo 9 foi um token copiado errado; o 404 do Passo 14 foi o GUID de exemplo do Swagger (`3fa85f64-...`) deixado no campo.
- **Facilidade no Swagger (17/11/2026):** o botão Authorize aceita **e-mail e senha** (OAuth2 "password" → `POST /api/v1/auth/token-swagger`, só em Development, oculto da lista) além de colar o token; `EnablePersistAuthorization` mantém o login após F5. Motivo: a usuária errou duas vezes ao copiar o JWT (token truncado → IDX14121).
- **Token em desenvolvimento (18/11/2026):** `appsettings.Development.json` define `Jwt:MinutosAcesso = 120` para facilitar os testes no Swagger (o 401 do Passo 13 foi o token de 15 min vencido). Em produção continua 15 min (appsettings.json). O frontend (Fase 10) deve renovar o token automaticamente com o refresh token.
- **Avisos do EF a revisar no futuro (não são erros):** "global query filter ... required end of a relationship" (ListaImportada→Itens, Orcamento→Opcoes, Usuario→RefreshToken/TokenRecuperacao) e "optional dependent using table sharing" (Endereco de Cliente/Fornecedor; o comportamento endereço todo nulo → null é o desejado).
- **Ainda não feito (previsto):** `GET /clientes/{id}/orcamentos` (depende dos orçamentos, Fase 7/10); criação automática do registro de Estoque ao cadastrar produto (Fase 8).

## ✅ Fase 4: o que foi implementado (17/11/2026)

- **Application:** `Abstracoes/ISeguranca.cs` (IHasherSenha, IServicoTokens com TokenAcessoGerado/TokenGerado, IEnvioEmail, IUsuarioAtual, IRepositorioAutenticacao); `Comum/Excecoes.cs` + `NaoAutorizadoException` (→ 401); `Autenticacao/` (Dtos, Validadores com `SenhaForte()`: ≥ 8 caracteres, letra e número, e `EmailValido()`, `ServicoAutenticacao`: registrar, login, refresh com rotação e detecção de reuso, logout, esqueci/redefinir senha); `Usuarios/Usuarios.cs` (DTOs, validadores, `ServicoUsuarios`: me, atualizar perfil, trocar senha, listar, obter, criar, alterar perfil e situação; o administrador não pode se rebaixar nem se desativar; desativar ou trocar senha encerra as sessões).
- **Infrastructure:** `Seguranca/HasherSenha.cs` (PasswordHasher do Identity, pacote `Microsoft.Extensions.Identity.Core 10.*`); `Persistencia/Repositorios/RepositorioAutenticacao.cs` (o ÚNICO lugar com `IgnoreQueryFilters()`); `Email/EnvioEmailSimulado.cs` (⚠️ só escreve no log; trocar por envio real antes da produção).
- **API:** pacote `Microsoft.AspNetCore.Authentication.JwtBearer 10.*`; `Seguranca/ConfiguracaoJwt.cs` (seção Jwt; a chave vem do user-secrets `Jwt:Chave`, ≥ 32 bytes, validada na inicialização; `ClaimsOrcaFacil`: sub, name, email, role, empresa_id); `Seguranca/ServicoTokensJwt.cs` (JsonWebTokenHandler, HMAC-SHA256; refresh e recuperação = 64 bytes aleatórios em Base64Url, guardados como SHA-256 hex); `Seguranca/UsuarioLogadoPorToken.cs` (implementa IEmpresaAtual e IUsuarioAtual; substituiu `EmpresaAtualPorToken.cs`, cujo arquivo a usuária deve excluir); `Configuracao/AutenticacaoExtensions.cs` (JwtBearer com MapInboundClaims=false, FallbackPolicy = exige login, políticas Gerente e Administrador, botão Authorize no Swagger); `AuthController` (público, rate limit em registrar/login/esqueci-senha) e `UsuariosController`; enums como texto no JSON; `/health` e `SistemaController` com AllowAnonymous; 401 no MapeadorDeExcecoes.
- **Testes:** `Tests/Application/Fakes.cs` (fakes em memória + RelogioFixo), `ServicoAutenticacaoTests` (16), `ServicoUsuariosTests` (10), `ValidadoresAutenticacaoTests`, `Tests/Infrastructure/HasherSenhaTests`, `Tests/Api/ServicoTokensJwtTests`, + 401 no mapeador.
- **Não precisa de migration:** as tabelas Usuarios, RefreshTokens e TokensRecuperacaoSenha já existiam.
- **Observação:** chamar `/auth/registrar` estando logado em outra empresa é bloqueado pela proteção multiempresa (o frontend não deve enviar token nessa chamada).

## ✅ Fase 3: o que foi implementado (17/11/2026)

- **Application:** `Comum/Excecoes.cs` (`NaoEncontradoException` → 404, `ConflitoException` → 409); `Comum/Paginacao.cs` (`ParametrosPaginacao` com correção automática: página ≥ 1, tamanho 1–100, padrão 20; `PaginaResultado<T>` com TotalPaginas/TemAnterior/TemProxima/Mapear); `Abstracoes/IRepositorio.cs` (`IRepositorio<T>` + `IUnitOfWork`); `DependencyInjection.AddAplicacao()` (registra os validadores do FluentValidation).
- **Infrastructure:** `Persistencia/Repositorios/Repositorio.cs` (genérico; ObterPorId com FirstOrDefault para respeitar o filtro de empresa; listagem AsNoTracking + Count + ordem estável por Id); `Persistencia/UnitOfWork.cs` (traduz concorrência, unique violation 23505 e FK violation 23503 em `ConflitoException`). Registrados em `AddInfraestrutura`.
- **API:** `Erros/MapeadorDeExcecoes.cs` (estático e testável) + `Erros/TratadorGlobalDeExcecoes.cs` (`IExceptionHandler`; 4xx → Warning, 5xx → Error); `AddProblemDetails` com `traceId`; `UseStatusCodePages`. Serilog (pacote `Serilog.AspNetCore 9.*`) configurado no appsettings: console + arquivo `logs/orcafacil-.log` (diário, 7 arquivos, 10 MB); em Development mostra o SQL do EF. `Configuracao/PoliticasApi.cs`: CORS `Frontend` (`Frontend:Origem`) e rate limiting `autenticacao` (5/min por IP → 429). `Controllers/SistemaController.cs`: `GET /api/v1/sistema/info` e `GET /api/v1/sistema/demonstracao-erro/{tipo}` (só em Development; didático). XML dos comentários incluído no Swagger. `Program.cs` reorganizado com try/catch/finally (ignora `HostAbortedException` do `dotnet ef`).
- **Testes:** `Tests/Application/PaginacaoTests.cs` e `Tests/Api/MapeadorDeExcecoesTests.cs` (o projeto de testes agora referencia a API).
- O aviso que aparecia nos builds anteriores sumiu: o build da Fase 3 terminou sem avisos.
- Logs em arquivo ficam em `backend/OrcaFacil.API/logs/` (o `dotnet run` usa a pasta do projeto como diretório de trabalho).

## Roteiro original da Fase 3

- Middleware global de exceções → ProblemDetails (`DomainException` → 422, validação → 400, não encontrado → 404, concorrência → 409, demais → 500 sem stack trace em produção).
- Serilog (console + arquivo rotativo), sem registrar dados sensíveis.
- Na Application: `IUnitOfWork`, repositórios genéricos/específicos, `Resultado`/paginação (`PaginaResultado<T>`, máx. 100 por página), registro do FluentValidation.
- Na Infrastructure: implementação dos repositórios e do UnitOfWork.
- CORS para `Frontend:Origem` e rate limiting (preparação para o login).
- Testes do middleware.

## Histórico de sessões

- **25/11/2026 — Sessão 8 (cont.):** Fase 8 escrita (decisões: preferir estoque; baixa ao finalizar), 246 testes OK na 1ª compilação. A pedido da usuária, **Claude testou a API sozinho pelo navegador embutido** (ela fez o login no painel). Todos os cenários conforme o esperado. Dados criados: estoques CAD10-1 = 8, CAD10-2 = 48, CAD10-3 = 10 (mínimo 20, Prateleira B2); ORC-2026-000003 (Comparativo) e ORC-2026-000004 (Unico, Finalizado).
- **25/11/2026 — Sessão 8:** validação da Fase 7 concluída no Swagger (D7–D12 e E13 conforme o esperado). 401 por token expirado guardado no navegador → Logout + Authorize. Documentação atualizada (guia 4F).
- **23/11/2026 — Sessão 7:** Fase 7 escrita. Erros: CS0411 (`OverridePropertyName` antes da regra) e um envio de arquivo desatualizado (commit em paralelo; ver lições). Migration revisada e aplicada. Senha do Postgres trocada por uma aleatória. Git instalado, **projeto publicado no GitHub**. Validação no Swagger até o passo C6 (400 por colar o empresaId no lugar da categoria).
- **18/11/2026 — Sessão 6 (cont.):** Fase 6 escrita e **validada**. O computador desligou por erro no meio da sessão: arquivos de código conferidos (110, íntegros); o `appsettings.Development.json` tinha voltado a uma versão anterior (OneDrive) e foi restaurado; `bin`/`obj` limpos antes do build. 400 no upload do Excel = campo opcional `clienteId` enviado com o GUID de exemplo do Swagger (validação do cliente acontece antes de ler o arquivo).
- **18/11/2026 — Sessão 6:** validação da Fase 5 concluída (149 testes). Ajustes durante a validação: login por e-mail e senha no Authorize do Swagger, EnablePersistAuthorization, token de 120 min só em Development, ordem do log de requisições corrigida. Erros encontrados pela usuária e explicados com o log: token copiado incompleto (IDX14121), GUID de exemplo do Swagger (404), token expirado (IDX10223).
- **17/11/2026 — Sessão 5:** Fase 5 escrita (cadastros de categorias, marcas, produtos com fornecedores e histórico de preços, fornecedores, clientes).
- **17/11/2026 — Sessão 4:** Fase 4 escrita e **validada** pela usuária (chave JWT no user-secrets, `EmpresaAtualPorToken.cs` excluído, testes OK, Swagger com registrar, Authorize, /me, 401, login e 429). Já existe no banco uma empresa de teste (Papelaria Exemplo, CNPJ 11.222.333/0001-81) com a administradora Aline. Próxima: Fase 5 (cadastros: produtos, categorias, marcas, fornecedores, clientes).
- **17/11/2026 — Sessão 3 (cont.):** Fase 3 escrita e **validada** (80 testes, build sem avisos, Swagger com `/sistema/info` e a demonstração de erros testados pela usuária). A usuária pediu passo a passo bem detalhado (uma ação por passo, com o resultado esperado e o que fazer se der errado). Manter esse formato. Próxima: Fase 4.
- **17/11/2026 — Sessão 3:** Fase 2.1 escrita e **validada**: 62 testes OK, banco recriado (`database drop` + `migrations add Inicial` + `database update`), `/health` Healthy. O build ainda mostra 1 aviso não identificado (investigar na Fase 3). Próxima: Fase 3.
- **16/11/2026 — Sessão 2 (final):** a usuária perguntou como acessar o Swagger (ainda vazio, sem controllers) e qual client usar para o PostgreSQL (recomendados: extensão PostgreSQL da Microsoft no VS Code e DBeaver para ver o diagrama ER). Ela observou que o sistema não deve ser focado em construção, pois quer vendê-lo a vários varejistas → **decidido SaaS multiempresa (Fase 2.1)**, para começar na próxima sessão.
- **16/11/2026 — Sessão 2:** a usuária pediu explicações didáticas (criado `docs/GUIA-DO-ESTUDANTE.md`). Escolheu Docker para aprender: Docker Desktop + WSL2 instalados, PostgreSQL 17 rodando via `docker compose`. User-secrets configurado. Problema: `AssemblyInfo.cs` vazio (disco cheio) → resolvido limpando `bin`/`obj`. Migration `Inicial` gerada (`Persistencia/Migrations/20261117003740_Inicial.cs`) e aplicada; certificado de desenvolvimento confiado (`dotnet dev-certs https --trust`); `/health` Healthy. **Fase 2 concluída.**
- **15/11/2026 — Sessão 1:** análise de requisitos, arquitetura (`docs/ARQUITETURA.md`), esqueleto da solução, domínio completo, 21 testes de domínio. FluentAssertions trocado por AwesomeAssertions. Projeto salvo na Área de Trabalho. Build e testes da Fase 1 validados pela usuária, depois de liberar espaço no disco C:. Fase 2 escrita (DbContext, 19 configurações, sequência, seed, DI, health check do banco, 16 testes de modelo).
