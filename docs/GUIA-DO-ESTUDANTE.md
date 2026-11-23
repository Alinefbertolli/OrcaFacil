# Guia do Estudante — Entendendo o OrçaFácil passo a passo

> Material de estudo que acompanha o projeto. Cada fase concluída ganha uma seção nova aqui.
> Leia com o código aberto ao lado no VS Code: o aprendizado acontece quando você liga a explicação ao arquivo.

---

## 0. A visão geral: o que estamos construindo?

O OrçaFácil é um **sistema web**. Todo sistema web costuma ter três partes. Pense num restaurante:

| Parte do sistema | No restaurante | No OrçaFácil | Tecnologia |
|---|---|---|---|
| **Frontend** | O salão e o cardápio: o que o cliente vê | As telas: login, dashboard, orçamentos | React + TypeScript (Fase 10) |
| **Backend** | A cozinha: onde o trabalho acontece | As regras: calcular orçamento, controlar estoque | C# com .NET |
| **Banco de dados** | A despensa: onde tudo fica guardado | Tabelas com produtos, clientes, orçamentos | PostgreSQL |

O cliente (frontend) faz um **pedido** à cozinha (backend) por meio de uma **API**, que funciona como o garçom. A cozinha busca ingredientes na despensa (banco) e devolve o prato pronto (os dados em formato **JSON**).

Estamos começando pela cozinha e pela despensa, porque as telas dependem delas.

---

## 1. As ferramentas

- **C#** é a linguagem de programação em que escrevemos o backend.
- **.NET** é a plataforma que compila e executa o C#. Você tem o SDK 10 instalado.
- **Terminal (PowerShell)** é onde você digita comandos. Os principais deste projeto:

| Comando | O que faz | Analogia |
|---|---|---|
| `cd backend` | Entra na pasta `backend` ("change directory") | Abrir uma gaveta |
| `cd ..` | Volta uma pasta | Fechar a gaveta |
| `dotnet build` | **Compila**: transforma o código C# em programa e aponta erros de escrita | Um revisor que lê o texto inteiro procurando erros |
| `dotnet test` | Roda os **testes automáticos** | Um checklist que confere se tudo funciona |
| `dotnet run --project OrcaFacil.API` | **Executa** a API | Abrir o restaurante |

> 💡 O primeiro erro que você viu (`dotnet run` na pasta errada) e o do `beckend` são os erros mais comuns de quem está começando, inclusive de profissionais. O terminal sempre trabalha **na pasta em que você está**. Olhe o caminho antes do `>` para saber onde você está.

> 💡 O erro de "espaço insuficiente" aconteceu porque o `dotnet build` baixa da internet as **bibliotecas** (códigos prontos feitos por outras pessoas) que o projeto usa. Esse download vem do **NuGet**, a "loja de bibliotecas" do .NET, e fica salvo no disco C:.

---

## 2. Por que dividir o projeto em fases?

Um sistema grande construído todo de uma vez vira uma bola de erros impossível de depurar. Construímos em camadas e **validamos cada uma** (build + testes) antes de seguir. Se algo quebra, sabemos que o problema está no que acabou de ser feito.

O arquivo `docs/CONTEXTO.md` é o nosso **diário de bordo**: registra onde paramos, o que foi decidido e o que vem depois. Ele existe porque a IA não lembra de conversas anteriores. É o arquivo que permite continuar o trabalho outro dia.

---

## 3. Fase 1: arquitetura e domínio

### 3.1 Arquitetura em camadas

Abra a pasta `backend`. Ela tem 5 projetos, e cada um tem uma responsabilidade:

```
OrcaFacil.API             → o "garçom": recebe pedidos da internet e devolve respostas
OrcaFacil.Application     → o "gerente": organiza o que precisa ser feito em cada pedido
OrcaFacil.Domain          → as "receitas": as regras do negócio
OrcaFacil.Infrastructure  → os "fornecedores": banco de dados, arquivos, serviços externos
OrcaFacil.Tests           → o "controle de qualidade"
```

**A regra de ouro:** o **Domain não depende de ninguém**. As regras do negócio ("não pode sair do estoque mais do que tem") não mudam se trocarmos o PostgreSQL por outro banco, ou a API web por um aplicativo de celular. Isolar as regras deixa o sistema mais fácil de testar e de manter. Esse princípio se chama **separação de responsabilidades** e é a base da *Clean Architecture*.

### 3.2 Entidades: as "coisas" do sistema

Uma **entidade** é uma classe que representa algo real do negócio: Produto, Cliente, Orçamento, Estoque. Abra `OrcaFacil.Domain/Entidades/Estoque.cs` e procure este trecho:

```csharp
public MovimentacaoEstoque RegistrarSaida(decimal quantidade, Guid usuarioId, string? motivo)
{
    Guarda.Positivo(quantidade, "Quantidade");
    if (quantidade > QuantidadeDisponivel)
        throw new DomainException("Saldo insuficiente...");

    return Movimentar(TipoMovimentacaoEstoque.Saida, quantidade, QuantidadeDisponivel - quantidade, ...);
}
```

Lendo linha a linha:
1. `public MovimentacaoEstoque RegistrarSaida(...)` é um **método** (uma ação que o estoque sabe fazer). Ele recebe a quantidade, quem está retirando e o motivo, e devolve um registro da movimentação.
2. `Guarda.Positivo(...)` verifica se a quantidade é maior que zero. Não faz sentido retirar 0 ou −5 sacos de cimento.
3. `if (quantidade > QuantidadeDisponivel)`: se pedirem mais do que existe…
4. `throw new DomainException(...)`: …o método **lança uma exceção**, ou seja, interrompe a operação com uma mensagem de erro.
5. Se passou pelas verificações, `Movimentar(...)` atualiza o saldo e cria o histórico.

Repare que `QuantidadeDisponivel` tem `private set`. Isso significa que **ninguém de fora da classe pode alterar o saldo diretamente**, só pelos métodos `RegistrarEntrada`, `RegistrarSaida` e `AjustarSaldo`, que sempre validam e registram o histórico. Esse conceito se chama **encapsulamento**, um dos pilares da Programação Orientada a Objetos que você vai estudar.

### 3.3 Enums

`Enums/Enumeracoes.cs` define listas fechadas de opções. Por exemplo, o status de um orçamento só pode ser `EmElaboracao`, `AguardandoAprovacao`, `Aprovado`, `Reprovado` ou `Finalizado`. Usar enum em vez de texto livre impede erros como digitar "aprovdo".

### 3.4 Testes automáticos

Abra `OrcaFacil.Tests/Domain/EstoqueTests.cs`. Todo teste segue o padrão **AAA**:

```csharp
[Fact]  // marca o método como um teste
public void Saida_maior_que_saldo_deve_ser_recusada()
{
    // Arrange (preparar): um estoque com 3 unidades
    var estoque = new Estoque(Guid.NewGuid(), 0);
    estoque.RegistrarEntrada(3, UsuarioId, null);

    // Act (agir): tentar retirar 4
    var acao = () => estoque.RegistrarSaida(4, UsuarioId, null);

    // Assert (verificar): deve dar erro e o saldo continua 3
    acao.Should().Throw<DomainException>();
    estoque.QuantidadeDisponivel.Should().Be(3);
}
```

Quando você rodou `dotnet test` e viu **"bem-sucedido: 40"**, o computador executou 40 verificações desse tipo em 3 segundos. Se um dia alguém alterar o código e quebrar uma regra, o teste avisa na hora. Testes são a **rede de segurança** do programador.

---

## 4. Fase 2: o banco de dados

### 4.1 O problema: objetos x tabelas

No C# trabalhamos com **objetos** (`new Produto()`). O PostgreSQL guarda **tabelas** (linhas e colunas, como uma planilha). Alguém precisa traduzir entre os dois.

Esse tradutor é o **Entity Framework Core (EF Core)**, um **ORM** (*Object-Relational Mapper*). Você escreve C#, e ele gera o SQL (a linguagem dos bancos de dados) por você.

### 4.2 Os arquivos da Fase 2

| Arquivo | Para que serve |
|---|---|
| `Infrastructure/Persistencia/OrcaFacilDbContext.cs` | A "porta de entrada" do banco. Cada `DbSet<Produto>` representa uma tabela. |
| `Infrastructure/Persistencia/Configuracoes/*.cs` | As regras de cada tabela: tamanho máximo dos textos, campos obrigatórios, índices, relacionamentos. |
| `Infrastructure/DependencyInjection.cs` | Registra o banco na aplicação (explicado abaixo). |
| `Tests/Infrastructure/ModeloBancoTests.cs` | Testes que geram o SQL e conferem se as tabelas estão certas, sem precisar do banco instalado. |

Exemplo de configuração (`CatalogoConfiguracoes.cs`):
```csharp
builder.Property(p => p.Nome).HasMaxLength(200).IsRequired();
builder.HasIndex(p => p.Codigo).IsUnique();
```
Em português: "o nome do produto tem no máximo 200 caracteres e é obrigatório" e "não podem existir dois produtos com o mesmo código".

### 4.3 Conceitos de banco de dados que aparecem aqui

- **Chave primária (PK):** o identificador único de cada linha, como o CPF de uma pessoa. Usamos `Id` do tipo `Guid`, um código aleatório como `0195f000-...`.
- **Chave estrangeira (FK):** uma coluna que aponta para outra tabela. `Produto.CategoriaId` diz a qual categoria o produto pertence.
- **Relacionamentos:** 1:N (uma categoria tem vários produtos), N:N (um produto tem vários fornecedores e um fornecedor vende vários produtos, ligados pela tabela `ProdutosFornecedores`) e 1:1 (cada produto tem um único registro de estoque).
- **Índice:** como o índice de um livro, acelera as buscas. Um índice **único** também impede repetições.
- **Restrição CHECK:** uma regra que o próprio banco garante, por exemplo `"QuantidadeDisponivel" >= 0`. Mesmo que um bug passe pelo C#, o banco recusa o saldo negativo. É uma segunda camada de proteção.

### 4.4 Migrations: o "controle de versão" do banco

Uma **migration** é um arquivo gerado pelo EF Core com as instruções para criar ou alterar as tabelas. Funciona como um "histórico de reformas" da despensa:
- `dotnet ef migrations add Inicial` → **gera** a migration (só cria um arquivo de código, não mexe no banco).
- `dotnet ef database update` → **aplica** as migrations no banco, ou seja, cria as tabelas de verdade.

Quando o modelo mudar no futuro (um campo novo, por exemplo), criamos outra migration. O banco evolui sem perder os dados.

### 4.5 Connection string e user-secrets: segurança

A **connection string** é o "endereço + senha" do banco: `Host=localhost;Database=orcafacil;Username=orcafacil;Password=...`

**Nunca** colocamos senhas dentro do código. Se o projeto for para o GitHub, qualquer pessoa veria a senha. Por isso usamos o **user-secrets**, um cofre do .NET que guarda a senha **fora da pasta do projeto**, só no seu computador.

### 4.6 Injeção de dependência (DI)

Em `Program.cs` aparece `builder.Services.AddInfraestrutura(...)`. Isso **registra** o banco num "catálogo de serviços" da aplicação. Quando alguma parte do código precisar do banco, ela pede ao catálogo em vez de criar a conexão sozinha. Assim, cada parte fica independente e fácil de testar. É o "D" do **SOLID** (*Dependency Inversion*).

---

### 4.7 Docker: o banco dentro de um "contêiner"

Em vez de instalar o PostgreSQL direto no Windows, vamos rodá-lo dentro do **Docker**.

| Conceito | O que é | Analogia |
|---|---|---|
| **Imagem** | Um "pacote" pronto com um programa e tudo de que ele precisa (ex.: `postgres:17-alpine`) | A receita congelada de um prato |
| **Contêiner** | Uma imagem em execução, isolada do resto do computador | O prato descongelado e servido |
| **Volume** | Uma pasta onde o contêiner guarda dados permanentes | A geladeira: os dados sobrevivem mesmo se o contêiner for desligado |
| **docker-compose.yml** | Arquivo que descreve quais contêineres subir e como configurá-los | A comanda com o pedido completo |

**Por que usar Docker?** Ele roda o mesmo ambiente em qualquer computador, não "suja" o Windows com instalações e permite apagar e recriar tudo em segundos. É muito usado nas empresas.

Abra o `docker-compose.yml` na raiz do projeto:
- `image: postgres:17-alpine` → qual imagem usar (PostgreSQL 17, versão enxuta).
- `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` → o contêiner cria automaticamente o banco e o usuário. A senha vem do arquivo `.env`, que não vai para o GitHub.
- `ports: "5432:5432"` → liga a porta 5432 do seu Windows à porta 5432 do contêiner, assim a API encontra o banco em `localhost:5432`.
- `volumes` → onde os dados ficam guardados.

Comandos do dia a dia (executados na **raiz** do projeto, onde está o `docker-compose.yml`):

| Comando | O que faz |
|---|---|
| `docker compose up -d` | Sobe o banco em segundo plano (`-d` = *detached*, libera o terminal) |
| `docker ps` | Lista os contêineres rodando |
| `docker compose logs postgres` | Mostra o que o banco está registrando (útil quando algo dá errado) |
| `docker compose stop` | Desliga o banco (os dados continuam salvos) |
| `docker compose down -v` | ⚠️ Remove o contêiner **e apaga os dados** (`-v` remove o volume) |

### 4.8 Lições aprendidas na Fase 2 (problemas reais que resolvemos)

| Sintoma | Causa | Solução | Lição |
|---|---|---|---|
| `Espaço insuficiente no disco` no build | O NuGet baixa as bibliotecas para o disco C:, que estava cheio | Liberar espaço | Mantenha espaço livre; ferramentas de desenvolvimento ocupam vários GB |
| `dotnet ef` dizia "Connection string não configurada", mesmo com o user-secrets salvo | O arquivo `obj\...\AssemblyInfo.cs` ficou vazio quando o disco encheu, e o **build incremental** não o refez | Apagar as pastas `bin` e `obj` e compilar de novo | Depois de disco cheio ou build interrompido, **limpe `bin`/`obj`**. Investigue a causa em vez de só tentar comandos |
| Chrome: "Sua conexão não é particular" | O certificado HTTPS de desenvolvimento não era confiável | `dotnet dev-certs https --trust` + reiniciar o Chrome | Certificados garantem a identidade de um site. Em `localhost`, quem gera o certificado é o próprio .NET |

Repare na segunda linha: o primeiro palpite (ambiente diferente de *Development*) estava **errado**. A solução apareceu ao olhar os arquivos gerados pelo build e encontrar um com 0 bytes. Depurar é isso: **formular uma hipótese, testar e, se não funcionar, buscar mais evidências.**

### 4.9 Rotina diária para subir o projeto

1. Abra o **Docker Desktop** e espere aparecer "Engine running".
2. Na pasta raiz `OrcaFacil`: `docker compose up -d` (sobe o banco).
3. Na pasta `backend`: `dotnet run --project OrcaFacil.API` (sobe a API).
4. Acesse https://localhost:7080/swagger (documentação da API) ou https://localhost:7080/health.
5. Para encerrar: **Ctrl+C** na API e `docker compose stop` na raiz.

---

## 4A. Fase 2.1: multiempresa (SaaS) e sistema neutro

### 4A.1 O que é SaaS multiempresa (*multi-tenancy*)

**SaaS** (*Software as a Service*) é o software vendido como serviço: o cliente não instala nada, acessa pela internet e paga uma assinatura. **Multiempresa** (*multi-tenant*, "vários inquilinos") quer dizer que **um único sistema e um único banco atendem muitas empresas ao mesmo tempo**.

Analogia: um **prédio de escritórios**. O prédio (o sistema) é um só, com uma só portaria e um só elevador, mas cada empresa tem a sua sala, e o crachá de cada funcionário só abre a porta da própria sala. No OrçaFácil:
- o **prédio** é o sistema e o banco PostgreSQL;
- cada **sala** é uma `Empresa` (a Papelaria X, a Loja Y);
- o **crachá** é o token de login, que diz de qual empresa o usuário é;
- a **fechadura** é o filtro automático `EmpresaId`.

### 4A.2 Como o isolamento funciona no código

**1) Toda tabela de dados do cliente ganhou a coluna `EmpresaId`.** Em vez de repetir essa propriedade em 13 classes, criamos uma classe-mãe (`Domain/Comum/EntidadeDaEmpresa.cs`), e as entidades passaram a **herdar** dela:

```csharp
public class Produto : EntidadeDaEmpresa   // antes era ": EntidadeBase"
```
Isso é **herança**, outro pilar da POO: `Produto` "é uma" `EntidadeDaEmpresa` e ganha de graça o `EmpresaId` e o método `VincularEmpresa`.

**2) O `EmpresaId` nunca muda depois de definido.** Veja o método `VincularEmpresa`: se o registro já pertence à empresa A, tentar passá-lo para a B lança um erro. Um produto nunca "pula" de uma loja para outra.

**3) Filtro global (a fechadura).** No `OrcaFacilDbContext.cs`:
```csharp
entidade.HasQueryFilter(e => e.EmpresaId == EmpresaIdAtual);
```
A partir disso, **toda** consulta, por exemplo `contexto.Produtos.ToList()`, vira automaticamente no banco:
```sql
SELECT ... FROM "Produtos" WHERE "EmpresaId" = '<empresa do usuário logado>'
```
O programador não precisa lembrar de filtrar, porque o filtro é aplicado sempre. Isso evita o pior bug possível num SaaS: uma loja ver os orçamentos de outra.

**4) Proteção ao salvar.** O método `GarantirIsolamentoEntreEmpresas` roda antes de cada gravação:
- registro novo sem empresa → recebe a empresa do usuário logado;
- registro de outra empresa → a gravação é **bloqueada**;
- sem usuário logado → nada é gravado.

Isso se chama **falhar fechado** (*fail closed*): na dúvida, o sistema nega o acesso em vez de liberar.

**5) Unicidade por empresa.** Antes, o código de produto "001" só podia existir uma vez no banco inteiro. Agora o índice único é composto `(EmpresaId, Codigo)`: a Papelaria X e a Loja Y podem, cada uma, ter o seu "001". O mesmo vale para a numeração dos orçamentos: cada empresa tem a sua sequência ORC-2026-000001, ORC-2026-000002...

### 4A.3 Sistema neutro quanto ao segmento

Removemos as 8 categorias de construção que eram criadas junto com o banco. Agora cada empresa cria as suas categorias. A empresa tem um campo `Segmento` (Papelaria, Informática...) que no futuro vai servir só para **sugerir** categorias, sem mudar nenhuma regra.

### 4A.4 Por que recriamos a migration em vez de criar outra

Numa aplicação com dados reais, **toda** mudança no banco vira uma migration nova, e as antigas nunca são apagadas, como as reformas de um prédio habitado. Como o nosso banco ainda não tinha nenhum dado, foi mais simples "demolir e reconstruir": apagar o banco e a migration antiga e gerar uma `Inicial` nova, já com o modelo multiempresa. **Isso só é aceitável em desenvolvimento, antes de existir dado real.**

---

## 4B. Fase 3: a infraestrutura da API (o "garçom" bem treinado)

### 4B.1 O pipeline: o caminho de cada requisição

Quando o navegador (ou o Swagger) chama a API, a requisição passa por uma **fila de etapas**, chamada **pipeline** ou **middlewares**, na ordem em que aparecem no `Program.cs`:

```
Requisição → [1 Tratador de erros] → [2 Páginas de status] → [3 Log] → [4 HTTPS] → [5 CORS] → [6 Limite] → Controller
Resposta   ←────────────────────────── volta pelo mesmo caminho ──────────────────────────────────────────────┘
```
A ordem importa. O tratador de erros é o **primeiro** para conseguir "pegar" um erro que aconteça em **qualquer** etapa depois dele, como uma rede de segurança embaixo de todo o resto.

### 4B.2 Erros padronizados (ProblemDetails)

Antes, se algo desse errado, cada parte do código responderia de um jeito. Agora existe **um único lugar** que decide a resposta: `API/Erros/MapeadorDeExcecoes.cs`.

| O que aconteceu | Exceção lançada | Código HTTP | Exemplo |
|---|---|---|---|
| Dados inválidos | `ValidationException` | **400** Bad Request | Nome do produto em branco |
| Registro não existe | `NaoEncontradoException` | **404** Not Found | Produto com Id inexistente |
| Conflito com o banco | `ConflitoException` | **409** Conflict | Código de produto repetido |
| Regra de negócio violada | `DomainException` | **422** Unprocessable Entity | Saída maior que o saldo |
| Bug (erro inesperado) | qualquer outra | **500** Internal Server Error | — |

Toda resposta de erro tem o mesmo formato JSON (o padrão internacional **RFC 7807**):
```json
{ "type": "...", "title": "Registro não encontrado.", "status": 404,
  "detail": "Produto não encontrado(a). Identificador: ...", "traceId": "0HN7..." }
```
- O **traceId** é um código único da requisição. Ele aparece também no log, então o suporte consegue achar exatamente o que aconteceu.
- Em **produção**, o erro 500 **esconde a mensagem técnica**, porque ela poderia revelar detalhes internos (nomes de tabelas, caminhos) a um atacante.

**Por que isso é bom?** Os controllers ficam limpos, sem `try/catch` espalhados: eles só lançam a exceção e o tratador responde. É o princípio da **responsabilidade única** (o "S" do SOLID).

### 4B.3 Logs com o Serilog

Um **log** é o "diário de bordo" do sistema. Configuramos o **Serilog** para escrever em dois lugares (`appsettings.json`, seção `"Serilog"`):
- **Console** (o terminal onde a API roda): para acompanhar ao vivo.
- **Arquivo** em `backend/OrcaFacil.API/logs/orcafacil-AAAAMMDD.log` (a pasta `logs` aparece dentro de `OrcaFacil.API` depois da primeira execução, porque o `dotnet run` executa a API a partir da pasta do projeto): um arquivo por dia, guardados por 7 dias e com no máximo 10 MB cada, para não encher o disco.

**Níveis de log**, do mais detalhado ao mais grave: `Debug` → `Information` → `Warning` → `Error` → `Fatal`. Em desenvolvimento ligamos o `Information` dos comandos do EF Core, então **você verá no terminal o SQL que o EF gera**. É ótimo para aprender SQL!

### 4B.4 Repositório e Unidade de Trabalho

- **Repositório** (`IRepositorio<T>`): uma "prateleira" com operações padrão para qualquer entidade: buscar por Id, verificar se existe, listar com paginação, adicionar e remover. O `<T>` é um **genérico**: a mesma classe serve para `Produto`, `Cliente`, `Fornecedor`... Escreve-se uma vez e usa-se em todo lugar.
- **Unidade de Trabalho** (`IUnitOfWork`): junta todas as alterações de uma operação e grava **de uma vez**, numa **transação**. Se algo falhar no meio, nada é gravado, como uma transferência bancária: ou o dinheiro sai de uma conta e entra na outra, ou nenhuma das duas coisas acontece.
- **Paginação:** listar 10 mil produtos de uma vez seria lento. A API devolve uma "página" por vez (no máximo 100 itens) e informa o total, para a tela montar os botões "anterior/próxima".

### 4B.5 Segurança básica

- **CORS:** por padrão, o navegador impede que um site chame uma API de outro endereço. Liberamos **somente** o endereço do nosso futuro frontend (`http://localhost:5173`).
- **Rate limiting** (limite de requisições): no máximo **5 tentativas por minuto** por IP no login (Fase 4). Isso dificulta ataques de **força bruta**, quando alguém tenta milhares de senhas até acertar. A 6ª tentativa recebe **HTTP 429** (*Too Many Requests*).

### 4B.6 Experimente no Swagger

Com a API rodando, abra https://localhost:7080/swagger:
1. **`GET /api/v1/sistema/info`** → **Try it out** → **Execute**. Você deve receber um **200** com o nome, a versão e o ambiente.
2. **`GET /api/v1/sistema/demonstracao-erro/{tipo}`** → teste com `validacao`, `nao-encontrado`, `conflito`, `regra` e `inesperado`. Compare os códigos HTTP e o JSON de cada um com a tabela da seção 4B.2. Depois olhe o terminal da API: cada erro aparece no log (avisos em amarelo, erros em vermelho).

---

## 4C. Fase 4: login, cadastro e permissões

### 4C.1 Autenticação x autorização

- **Autenticação** responde **"QUEM é você?"**: você prova a identidade com e-mail e senha.
- **Autorização** responde **"O QUE você pode fazer?"**: um Operador não pode criar usuários, um Administrador pode.

### 4C.2 Senhas: nunca guardadas, só o "hash"

O banco **não guarda a senha**. Guarda um **hash**: o resultado de uma conta matemática de mão única. É fácil ir de `Senha123` ao hash, mas impossível voltar do hash para `Senha123`.

No login, calculamos o hash da senha digitada e comparamos com o hash guardado. Se baterem, a senha está certa.

Detalhes de segurança que usamos (em `Infrastructure/Seguranca/HasherSenha.cs`):
- **Sal:** um valor aleatório misturado à senha. Duas pessoas com a mesma senha têm hashes diferentes.
- **Lentidão proposital:** 100 mil repetições. Para o usuário, o login demora uma fração de segundo; para um atacante que queira testar bilhões de senhas, o ataque fica impraticável.

### 4C.3 O "crachá digital": JWT

Depois do login, a API entrega um **token JWT**, um texto longo com três partes separadas por ponto: `cabeçalho.conteúdo.assinatura`.
- O **conteúdo** traz as *claims*: quem é o usuário (`sub`), o perfil (`role`) e a empresa (`empresa_id`).
- A **assinatura** é feita com uma **chave secreta** que só a API conhece. Se alguém alterar o conteúdo, por exemplo trocando a empresa, a assinatura deixa de bater e o token é recusado.

O conteúdo é apenas codificado, **não criptografado**: qualquer um consegue ler (experimente colar um token em https://jwt.io). Por isso **nunca** colocamos senhas ou dados sensíveis no JWT.

A cada requisição, o navegador envia o token no cabeçalho `Authorization: Bearer <token>`. A API confere a assinatura, lê a `empresa_id`, e o filtro multiempresa da Fase 2.1 passa a funcionar de verdade.

### 4C.4 Dois tokens: acesso e refresh

| | Token de acesso (JWT) | Refresh token |
|---|---|---|
| Duração | **15 minutos** | **7 dias** |
| Para que serve | Acompanha toda requisição | Pedir um novo token de acesso sem digitar a senha |
| Se for roubado | Logo deixa de funcionar | É **rotativo**: cada uso gera um novo e invalida o anterior. Se um token já usado aparecer de novo, todas as sessões do usuário são encerradas |
| Onde fica no banco | Em lugar nenhum (a assinatura basta) | Só o **hash** dele |

### 4C.5 Cuidados contra ataques

- **Mensagem genérica:** no login, "e-mail ou senha inválidos" vale para os dois casos, para o atacante não descobrir quais e-mails existem.
- **Mesmo tempo de resposta:** quando o e-mail não existe, calculamos um hash mesmo assim, para a resposta demorar o mesmo tempo.
- **Limite de tentativas:** 5 por minuto por IP (o *rate limiting* da Fase 3).
- **"Esqueci minha senha":** a resposta é sempre a mesma, exista ou não o e-mail. O link vale **1 hora** e funciona **uma vez só**.

### 4C.6 Permissões por perfil

| Ação | Operador | Gerente | Administrador |
|---|:---:|:---:|:---:|
| Ver e editar o próprio perfil, trocar a própria senha | ✔ | ✔ | ✔ |
| Listar os usuários da empresa | | ✔ | ✔ |
| Criar usuários, mudar perfil, ativar e desativar | | | ✔ |

No código, isso aparece como um **atributo** em cima do método do controller:
```csharp
[Authorize(Policy = PoliticasApi.Administrador)]
public async Task<ActionResult<UsuarioResposta>> Criar(...)
```
Quem não tem permissão recebe **403 Forbidden**; quem não está logado recebe **401 Unauthorized**.

### 4C.7 Testes com "fakes"

Os serviços de login dependem de **interfaces** (`IRepositorioAutenticacao`, `IHasherSenha`...). Nos testes, trocamos as implementações reais por versões **falsas e simples** (`Tests/Application/Fakes.cs`), que guardam tudo em listas na memória. Assim testamos a lógica inteira, inclusive "avançar o relógio 8 dias" para ver o refresh token expirar, sem banco de dados e em milissegundos. Isso é a **inversão de dependência** (o "D" do SOLID) na prática.

---

## 4D. Fase 5: os cadastros (CRUD)

### 4D.1 O que é um CRUD

Quase todo sistema tem telas para **C**riar, **R**ead (consultar), **U**pdate (alterar) e **D**elete (excluir) registros. Em REST, cada operação usa um **verbo HTTP**:

| Operação | Verbo | Endereço | Resposta de sucesso |
|---|---|---|---|
| Listar | `GET` | `/api/v1/produtos?busca=caderno&pagina=1` | 200 + página de resultados |
| Consultar um | `GET` | `/api/v1/produtos/{id}` | 200 |
| Criar | `POST` | `/api/v1/produtos` | **201 Created** + cabeçalho `Location` |
| Alterar | `PUT` | `/api/v1/produtos/{id}` | 200 |
| Excluir/desativar | `DELETE` | `/api/v1/produtos/{id}` | **204 No Content** (deu certo e não há nada a devolver) |

Os 5 cadastros (categorias, marcas, produtos, fornecedores e clientes) seguem **o mesmo padrão**. Quem aprende um, entende todos.

### 4D.2 O caminho de um cadastro

```
JSON → Controller → Serviço → Validador → Regras (duplicidade, existência) → Repositório → UnitOfWork → banco
```
1. **Validador (FluentValidation):** formato. O nome está preenchido? O CNPJ é válido? A UF tem 2 letras? Se não, **400**.
2. **Serviço:** regras que dependem do banco. Já existe outro produto com esse código? A categoria informada existe? Se houver conflito, **409**.
3. **Padronização:** a unidade "un" vira "UN"; o CNPJ "11.222.333/0001-81" vira "11222333000181"; o e-mail vai para minúsculas. Assim, a busca e a comparação funcionam sempre.

### 4D.3 Excluir de verdade x desativar

| | Exclusão **física** | Exclusão **lógica** (desativar) |
|---|---|---|
| O que acontece | O registro some do banco (`DELETE`) | O campo `Ativo` vira `false` |
| Onde usamos | **Categorias e marcas**, e só se não estiverem em uso | **Produtos, fornecedores e clientes** |
| Por quê | São só "etiquetas"; se ninguém usa, podem sumir | Orçamentos antigos, histórico de preços e estoque apontam para eles. Apagá-los quebraria o passado |

Um produto desativado some das listas (a não ser com `incluirInativos=true`) e pode ser **reativado**.

### 4D.4 Relacionamento N:N com dados: produto x fornecedor

Um produto pode ser vendido por vários fornecedores, e um fornecedor vende vários produtos. Isso é um relacionamento **N:N**, resolvido com uma tabela no meio: `ProdutosFornecedores`. Ela guarda **mais do que a ligação**: o preço daquele fornecedor, o código do produto no catálogo dele e o prazo de entrega. Essa é a base para comparar preços na Fase 7.

`PUT /api/v1/produtos/{id}/fornecedores/{fornecedorId}` cria a ligação ou atualiza a que já existe.

### 4D.5 Histórico de preços

Toda vez que um preço **muda**, seja o de referência digitado no produto ou o de um fornecedor, um registro é gravado em `HistoricoPrecos` com o valor, a **origem** (`Manual` ou `FornecedorCadastrado`) e a data. Se o preço não mudou, nada é registrado, para não poluir o histórico. Os relatórios de "evolução de preços" (Fase 12) vão usar esses dados.

### 4D.6 O "problema N+1" (e como evitamos)

A lista de produtos mostra o **nome da categoria** de cada produto. O jeito ingênuo seria, para cada um dos 20 produtos da página, buscar a categoria no banco: **1 consulta para a lista + 20 para as categorias = 21 consultas**. Com 100 itens, seriam 101 consultas. É o famoso **problema N+1**.

A solução está no método `ListarPorIdsAsync`: juntamos os Ids das categorias da página e buscamos **todas de uma vez** (`WHERE "Id" IN (...)`). Resultado: **3 consultas**, não importa o tamanho da página (produtos, categorias e marcas).

Experimente: com a API rodando, liste produtos no Swagger e olhe o **terminal**. Em desenvolvimento, o SQL de cada consulta aparece lá.

### 4D.7 Teste no Swagger (roteiro sugerido)

Antes, clique em **Authorize 🔒**. No bloco **EmailESenha**, digite o seu e-mail em `username` e a senha em `password` (deixe `client_id` e `client_secret` em branco) → **Authorize** → **Close**. O Swagger faz o login e guarda o token sozinho, e ele continua valendo mesmo depois de um F5. Depois:
1. `POST /api/v1/categorias` → `{ "nome": "Cadernos" }` → copie o `id`.
2. `POST /api/v1/marcas` → `{ "nome": "Tilibra", "nivelReconhecimento": 4 }` → copie o `id`.
3. `POST /api/v1/produtos` com o código, o nome, a unidade, o `categoriaId` e o `marcaId` → **201**.
4. `POST /api/v1/fornecedores` com a razão social e um CNPJ válido → copie o `id`.
5. `PUT /api/v1/produtos/{id}/fornecedores/{fornecedorId}` → `{ "preco": 19.90, "prazoEntregaDias": 5 }`.
6. `GET /api/v1/produtos/{id}` → veja o fornecedor com o preço.
7. `GET /api/v1/produtos/{id}/historico-precos` → veja o histórico.
8. Tente cadastrar o mesmo código de produto de novo → **409**.

---

## 4E. Fase 6: importação inteligente de listas

### 4E.1 O fluxo completo

```
 Arquivo ou texto
       │
       ▼
 1. VALIDAR ──────── tamanho ≤ 10 MB, extensão permitida, "assinatura" do arquivo
       │
       ▼
 2. GUARDAR ──────── cópia do original em storage/uploads (nome aleatório)
       │
       ▼
 3. EXTRAIR TEXTO ── .txt/WhatsApp → leitura direta
       │             Excel → ClosedXML     Word → OpenXml     PDF → PdfPig
       │             Imagem / PDF escaneado → OCR (Tesseract)
       ▼
 4. INTERPRETAR ──── cada linha vira: quantidade + unidade + descrição + marca + confiança
       │
       ▼
 5. SUGERIR PRODUTO ─ busca por nome parecido no catálogo (pg_trgm)
       │
       ▼
 6. REVISÃO (obrigatória) ── corrigir, excluir, adicionar, escolher produto → CONFIRMAR
```

Cada etapa está numa classe diferente (responsabilidade única), e o `ServicoImportacao` é o "maestro" que chama uma depois da outra.

### 4E.2 A "assinatura" dos arquivos (magic bytes)

Todo tipo de arquivo começa com alguns bytes característicos: PDF começa com `%PDF`, PNG com `89 50 4E 47`, e Excel e Word com `PK` (por dentro, eles são arquivos ZIP!). O `ValidadorArquivo` confere esses bytes. Assim, um programa malicioso renomeado para `lista.pdf` é **recusado**, porque a extensão mente, mas o conteúdo não.

### 4E.3 O interpretador: expressões regulares

O `InterpretadorLista` usa **expressões regulares** (regex), padrões que descrevem textos. Exemplo simplificado:

```
^(?<qtd>\d+)\s*(?<un>cx|un|kg)?\s+(?<descricao>.+)$
```
Lendo: "no início (`^`), um número (`\d+`) que chamaremos de *qtd*; espaços opcionais (`\s*`); talvez uma unidade; espaços; e o resto até o fim (`.+$`) é a *descricao*".

Ele entende formatos variados:

| Linha | Quantidade | Unidade | Descrição | Marca |
|---|---|---|---|---|
| `20 cx caneta azul BIC` | 20 | CX | caneta azul | BIC (marca cadastrada) |
| `caneta azul - 20 cx` | 20 | CX | caneta azul | — |
| `5 resmas de papel A4` | 5 | RESMA | papel A4 | — |
| `caderno 200 folhas` | 1 ⚠️ | UN | caderno 200 folhas | — |
| `3 kits de pintura marca Acrilex` | 3 | KIT | pintura | Acrilex |

Repare em `caderno 200 folhas`: "folhas" não é unidade, então o 200 faz parte da descrição. Na dúvida, o sistema **não inventa**: assume 1 e marca o item com **confiança baixa**, para a revisão.

E ignora o que não é item: "Bom dia!", "Segue a lista:", "Obrigada!", "<Mídia oculta>", emojis, os prefixos do WhatsApp (`[10:32, 18/11/2026] Cliente:`) e os marcadores (`-`, `•`, `1.`).

### 4E.4 Confiança e "precisa de atenção"

Cada item recebe uma nota de 0 a 1:
- 0,4 de base, +0,3 se tinha quantidade, +0,1 se reconheceu a unidade, +0,05 se reconheceu a marca;
- +0,15 se encontrou um produto parecido no catálogo.

Um item **precisa de atenção** se a confiança ficou abaixo de 0,7 **ou** se não foi associado a nenhum produto do catálogo. A tela de revisão (Fase 10) vai destacar esses itens.

### 4E.5 Busca por similaridade: trigramas

Como saber que "canetas azul" é parecido com "Caneta esferográfica azul"? O PostgreSQL (extensão **pg_trgm**, instalada na Fase 2) quebra os textos em pedaços de 3 letras, os **trigramas**: "caneta" → `can`, `ane`, `net`, `eta`... Quanto mais pedaços em comum, maior a **similaridade** (de 0 a 1). A partir de **0,5**, o produto é sugerido automaticamente.

### 4E.6 OCR: ler texto de imagens

**OCR** (*Optical Character Recognition*) é o reconhecimento de letras numa imagem. Usamos o **Tesseract**, gratuito, que roda no próprio servidor. Ele precisa de um "dicionário" do idioma (`por.traineddata`), que você baixa uma vez (veja o README).

Limitações: funciona bem com **texto impresso** e **fotos nítidas e retas**. Letra cursiva, fotos tortas ou escuras dão resultados ruins. Mais um motivo para a revisão ser obrigatória.

### 4E.7 Testes com arquivos de verdade

Em `Tests/Infrastructure/ExtratorTextoTests.cs`, os testes **criam arquivos reais em memória**: uma planilha Excel com cabeçalho, um documento Word com parágrafo e tabela, e um PDF com duas linhas de texto. Depois conferem o texto extraído. Assim, testamos as bibliotecas de verdade sem precisar de arquivos guardados no projeto.

---

## 5. Como trabalhar bem com um agente de IA (dicas para estudantes)

1. **Não aceite código que você não entende.** Pergunte "por que isso?" e "o que essa linha faz?". Entender é o seu objetivo, e o código é consequência.
2. **Rode você mesma os comandos** e leia as mensagens, inclusive os erros. Ler mensagens de erro é uma das habilidades mais importantes da profissão.
3. **Erros são normais.** A IA também erra. Por isso compilamos e testamos a cada fase.
4. **Valide em pequenos passos.** Esse foi o motivo de dividirmos em fases.
5. **Mantenha o contexto atualizado** (`CONTEXTO.md`). Na profissão, isso equivale a documentar o projeto.
6. **Use o projeto para estudar as matérias do curso:** POO (classes, encapsulamento), Banco de Dados (tabelas, chaves, SQL), Engenharia de Software (arquitetura, testes).

---

## 6. Glossário rápido

| Termo | Significado |
|---|---|
| API | Interface que permite a programas conversarem entre si pela internet |
| Endpoint | Um "endereço" da API, ex.: `GET /api/v1/produtos` |
| JSON | Formato de texto para trocar dados: `{ "nome": "Cimento" }` |
| Classe | Molde que define um tipo de objeto (atributos + métodos) |
| Objeto | Uma "instância" de uma classe (`new Produto()`) |
| Método | Uma ação que a classe sabe executar |
| Exceção | Um erro lançado de propósito para interromper uma operação inválida |
| Compilar | Traduzir o código-fonte para um programa executável, checando erros |
| Biblioteca / pacote | Código pronto que reutilizamos (via NuGet) |
| ORM | Ferramenta que traduz objetos ↔ tabelas (EF Core) |
| Migration | Script versionado que cria ou altera o banco |
| Health check | Endereço que informa se o sistema está saudável (`/health`) |
