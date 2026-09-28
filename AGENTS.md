# LogTrack / Extensionista 4 — Regras de Arquitetura

Este projeto é o LogTrack / Extensionista 4, desenvolvido em .NET MAUI + ASP.NET Core Web API + Entity Framework Core + SQL Server.

Estas regras devem ser consultadas antes de alterações arquiteturais, criação de arquivos, refatorações ou implementação de funcionalidades.

## 1. Estrutura da solução

```text
Aplicativo.sln
├── Aplicativo.Core
├── Aplicativo.Api
└── Aplicativo.Mobile
```

`Aplicativo.Api` e `Aplicativo.Mobile` podem referenciar `Aplicativo.Core`. O `Aplicativo.Core` nunca deve referenciar API, Mobile, ASP.NET Core, Entity Framework Core, SQL Server, .NET MAUI, Android, iOS ou Scalar.

A direção das dependências é:

```text
Aplicativo.Api ──────┐
                     ▼
               Aplicativo.Core
                     ▲
Aplicativo.Mobile ───┘
```

## 2. Aplicativo.Core

Estrutura preferencial:

```text
Aplicativo.Core
├── Domain
│   ├── Enum
│   └── Model
├── Application
│   ├── Command
│   └── DTO
└── Common
    ├── Constants
    └── Validation
```

Devem ficar no Core os Models, Enums, DTOs, Commands/Requests compartilhados, constantes e validações realmente compartilháveis.

Exemplos de Models:

```text
Usuario.cs
Veiculo.cs
Viagem.cs
Rota.cs
Movimentacao.cs
Categoria.cs
FormaPagamento.cs
Divida.cs
Manutencao.cs
```

## 3. Commands e DTOs

Contracts de entrada compartilhados devem ficar no Core:

```text
Command       → Aplicativo.Core/Application/Command
CommandHandler → Aplicativo.Api/Application/CommandHandler
DTOs compartilhados → Aplicativo.Core/Application/DTO
```

Handlers dependentes de infraestrutura não devem ficar no Core. Evitar duplicar DTOs entre API e Mobile e evitar expor entidades do banco diretamente quando um DTO for mais apropriado.

## 4. Aplicativo.Api

Estrutura preferencial:

```text
Aplicativo.Api
├── Application
│   ├── CommandHandler
│   ├── Query
│   └── Services
├── Configuration
├── Controllers
├── Data
│   ├── Context
│   ├── Mapping
│   └── Repository
├── Migrations
├── appsettings.json
└── Program.cs
```

A API contém Controllers, Handlers, Queries, Services de backend, DbContext, Repositories, mappings do EF Core, migrations, OpenAPI, Scalar, injeção de dependência, JWT e validação do token Google.

Controllers devem ser enxutos. Regras de negócio complexas devem ficar em Services, Handlers ou componentes da camada Application.

## 5. Scalar e OpenAPI

O projeto utiliza Scalar para documentação e testes da API. Não utilizar Swagger UI como padrão.

Configurações de Scalar/OpenAPI ficam exclusivamente na API, preferencialmente em:

```text
Aplicativo.Api/Configuration/ScalarConfig.cs
```

Core e Mobile não devem depender de Scalar ou OpenAPI específico da API.

## 6. Entity Framework Core e SQL Server

O banco oficial é Microsoft SQL Server, utilizando Entity Framework Core e Migrations.

- Entidades: `Aplicativo.Core/Domain/Model`.
- DbContext: `Aplicativo.Api/Data/Context`.
- Mappings Fluent API: `Aplicativo.Api/Data/Mapping`.
- Repositories: `Aplicativo.Api/Data/Repository`.
- Migrations: exclusivamente em `Aplicativo.Api/Migrations`.

Evitar Data Annotations específicas do EF Core nas entidades do Core. Preferir Fluent API. Alterações estruturais devem ser feitas por migrations.

O Mobile não deve conter lógica de persistência do SQL Server.

## 7. Aplicativo.Mobile

Estrutura preferencial:

```text
Aplicativo.Mobile
├── Views
├── ViewModels
├── Services
│   ├── Api
│   ├── Authentication
│   └── Synchronization
├── Data
│   └── Local
├── Platforms
│   ├── Android
│   └── iOS
├── Resources
└── MauiProgram.cs
```

No Mobile ficam Views, ViewModels, chamadas HTTP, integração com API, Credential Manager, SecureStorage, armazenamento local, sincronização offline-first e código específico de plataforma.

Código Android deve ficar em `Platforms/Android`; código iOS deve ficar em `Platforms/iOS`.

## 8. Google SSO

No Android, utilizar Credential Manager como padrão para Sign in with Google. Não utilizar o GoogleSignInClient legado como nova implementação.

WebAuthenticator somente deve ser utilizado quando houver necessidade arquitetural específica de um fluxo OAuth baseado em navegador.

Fluxo esperado:

```text
Android/MAUI
→ Credential Manager
→ Google
→ Google ID Token
→ Aplicativo.Api
→ validação do token
→ identificação/localização do usuário
→ regras de RBAC
→ JWT próprio do LogTrack
→ SecureStorage no Mobile
```

O Google comprova a identidade. A API e o banco do LogTrack controlam autorização e permissões. Permissões do Google não substituem o RBAC do LogTrack.

## 8.1 Offline-first e SQLite no Mobile

O aplicativo Mobile deve seguir o padrão **offline-first**:

- O SQLite local é o armazenamento operacional do Mobile e deve ficar em `FileSystem.AppDataDirectory`.
- A interface deve ler primeiro do SQLite e as operações de criação/edição devem ser gravadas localmente antes de tentar sincronizar com a API.
- Operações feitas sem conexão devem ser registradas em uma fila local de sincronização (outbox), com status, tentativas e mensagem de erro.
- A sincronização com a API deve ser assíncrona, retomável e idempotente; falhas de rede não podem apagar dados locais nem bloquear o uso básico do aplicativo.
- O SQL Server é o banco central da API. O Mobile nunca deve acessar SQL Server diretamente.
- Modelos/tabelas exclusivos do SQLite, repositórios locais e a fila de sincronização pertencem ao `Aplicativo.Mobile/Data/Local`; não devem ser colocados no `Aplicativo.Core`.
- O banco local deve ter versionamento/migração controlado pelo Mobile; alterações de schema não devem depender de uma chamada à API.
- O token JWT e a sessão continuam no `SecureStorage`; não salvar secrets ou tokens na fila de sincronização.

Infraestrutura inicial esperada:

```text
Aplicativo.Mobile
└── Data
    └── Local
        ├── LocalDatabase.cs
        ├── LocalDatabaseMetadata.cs
        └── PendingSyncOperation.cs
```

## 9. RBAC

Os perfis e permissões são controlados pelo LogTrack:

- Administrador
- Gestor de Frota
- Financeiro
- Operador/Motorista

O Google não define o perfil do usuário. No primeiro login, o usuário pode permanecer como `AguardandoAprovacao` até a aprovação e definição de perfil por um Administrador.

Toda autorização deve ser validada no backend. Ocultar funcionalidades no Mobile não substitui a autorização na API.

## 10. Regra de compartilhamento

Antes de criar uma classe, avaliar se ela pode ser utilizada pela API e pelo Mobile. Se puder, considerar colocá-la no Core.

Podem pertencer ao Core: Models, Enums, DTOs, Commands, Requests, Responses compartilhados, constantes e validações compartilháveis.

Não pertencem ao Core: DbContext, repositories dependentes de EF Core, mappings, migrations, Controllers, HttpContext, JWT, Scalar, OpenAPI específico da API, Credential Manager, SecureStorage, SQLite específico do Mobile, Views e ViewModels.

## 11. Objetivo arquitetural

Evitar duplicação de código e contratos entre API e Mobile. Uma alteração em Model, DTO ou Command compartilhado no Core deve ser enxergada pelos projetos que o referenciam.

Isso não significa que toda alteração no Core modifique automaticamente telas, banco local ou migrations; cada responsabilidade deve permanecer em sua camada.

## 12. Regras para alterações

Estas regras devem ser respeitadas ao:

- Criar arquivos, pastas, entidades, DTOs e Commands.
- Implementar endpoints, Handlers e Services.
- Trabalhar com autenticação e RBAC.
- Configurar Entity Framework Core, SQL Server e migrations.
- Configurar OpenAPI e Scalar.
- Implementar funcionalidades MAUI.
- Implementar sincronização offline-first.
- Integrar API e Mobile.

Se uma solicitação entrar em conflito com este padrão, informar o conflito antes de realizar alteração arquitetural significativa.

## 13. Documentação desta arquitetura

Este arquivo é a referência técnica do repositório para agentes, ferramentas e desenvolvedores.

Quando uma nova decisão arquitetural for explicitamente definida pelo responsável técnico, atualizar este arquivo.

Não colocar neste arquivo credenciais, tokens, Client Secrets, senhas, connection strings reais ou qualquer outro dado sensível.
