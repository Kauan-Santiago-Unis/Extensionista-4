# LogTrack — Extensionista 4

Aplicativo multiplataforma para controle financeiro e gerenciamento de frota logística. O projeto é desenvolvido como uma solução composta por aplicativo MAUI, API REST e contratos compartilhados.

## Objetivo

O LogTrack deverá permitir o controle de:

- veículos e informações da frota;
- viagens, rotas, hodômetro e manutenção;
- receitas, despesas, categorias e formas de pagamento;
- custos operacionais e indicadores financeiros;
- sincronização dos dados quando o dispositivo voltar a ter conexão.

## Stack tecnológica

- **Aplicativo:** C# com .NET MAUI 10.
- **API:** ASP.NET Core Web API 10.
- **Contratos compartilhados:** projeto `Aplicativo.Core`.
- **Banco central:** Microsoft SQL Server, acessado pela API com Entity Framework Core e migrations.
- **Banco local:** SQLite no aplicativo MAUI, usando `sqlite-net-pcl`.
- **Autenticação:** Google OAuth 2.0/OpenID Connect e JWT próprio do LogTrack.
- **Documentação da API:** OpenAPI com Scalar.
- **Versionamento:** Git e GitHub.
- **Organização:** Trello.
- **Prototipação:** Figma.

## Estrutura da solução

```text
Extensionista_4/
├── AGENTS.md
├── README.md
└── Extensionista_4/
    ├── Extensionista_4.slnx
    └── src/
        ├── Aplicativo.Api/
        ├── Aplicativo.Core/
        └── Aplicativo.Mobile/
```

### Aplicativo.Core

Contém os contratos compartilhados entre API e Mobile: entidades de domínio, enums, DTOs, commands, constantes e validações compartilháveis.

O Core não deve referenciar ASP.NET Core, Entity Framework Core, SQL Server, .NET MAUI ou APIs específicas de Android/iOS.

### Aplicativo.Api

Contém controllers, services, handlers, validação do Google, JWT, OpenAPI/Scalar e, na etapa de persistência central, DbContext, mappings, repositories e migrations do SQL Server.

### Aplicativo.Mobile

Contém as telas, ViewModels, chamadas HTTP, autenticação, SecureStorage, armazenamento local SQLite e sincronização offline-first.

## Arquitetura de autenticação

```text
Google
   ↓
Aplicativo.Mobile
   ↓ Google ID Token
Aplicativo.Api
   ↓ validação do token Google
JWT próprio do LogTrack
   ↓
SecureStorage do dispositivo
```

O Google confirma a identidade. A API é responsável por autorização, perfis e permissões do LogTrack.

### Android

O Android utiliza o Credential Manager com o Client ID Web como `serverClientId`. O Client ID Android deve estar associado ao pacote e ao SHA-1 do certificado de assinatura utilizado.

### Windows

O Windows utiliza o navegador padrão e o fluxo OAuth2 com PKCE e retorno loopback em `127.0.0.1`. É necessário um Client ID OAuth do tipo **Desktop app**.

O Client ID Desktop é público e fica no Mobile. O Client Secret não deve ser versionado; a solução definitiva deve fazer a troca do authorization code dentro da API, usando User Secrets ou variáveis de ambiente.

### iOS e MacCatalyst

Esses alvos ainda precisam de configuração específica de Client ID, Bundle ID e retorno OAuth antes de serem considerados prontos para teste.

Mais detalhes estão em [AUTHENTICATION.md](Extensionista_4/AUTHENTICATION.md).

## Estratégia offline-first

O Mobile deve continuar funcional mesmo sem conexão.

- As telas devem ler primeiro do SQLite local.
- Criações e alterações devem ser salvas localmente antes da sincronização.
- Alterações offline devem entrar em uma fila outbox local.
- A sincronização deve ser assíncrona, retomável e idempotente.
- Falhas de rede não devem apagar dados locais.
- O Mobile nunca acessa o SQL Server diretamente.
- O JWT e a sessão ficam no `SecureStorage`, não na fila de sincronização.

### SQLite local

O banco local é criado como `logtrack.db3` em `FileSystem.AppDataDirectory`. A infraestrutura está em:

```text
Aplicativo.Mobile/Data/Local/
├── LocalDatabase.cs
├── LocalDatabaseMetadata.cs
└── PendingSyncOperation.cs
```

Para criar uma tabela local, use a conexão por meio do serviço `LocalDatabase`:

```csharp
var connection = await localDatabase.GetConnectionAsync();
await connection.CreateTableAsync<SeuModeloLocal>();
await connection.InsertAsync(entidade);
```

Modelos exclusivos do SQLite devem permanecer no Mobile, e não no `Aplicativo.Core`.

## Pré-requisitos

- Visual Studio com workload **.NET Multi-platform App UI development**.
- .NET SDK 10.
- Android SDK e um emulador ou dispositivo Android.
- Windows 10/11 para executar o alvo Windows.
- SQL Server para a etapa de persistência central da API.
- Conta/projeto configurado no Google Cloud para os Client IDs OAuth.

## Configuração local da API

Os valores sensíveis devem ser configurados com User Secrets e não no `appsettings.json`.

Na pasta `Extensionista_4`, execute:

```powershell
dotnet user-secrets set `
  "Google:AllowedClientIds:0" `
  "SEU_CLIENT_ID_WEB.apps.googleusercontent.com" `
  --project ".\src\Aplicativo.Api\Aplicativo.Api.csproj"

dotnet user-secrets set `
  "Google:AllowedClientIds:1" `
  "SEU_CLIENT_ID_DESKTOP.apps.googleusercontent.com" `
  --project ".\src\Aplicativo.Api\Aplicativo.Api.csproj"

dotnet user-secrets set `
  "Jwt:Key" `
  "SUA_CHAVE_LOCAL_COM_PELO_MENOS_32_CARACTERES" `
  --project ".\src\Aplicativo.Api\Aplicativo.Api.csproj"
```

Não versionar Client Secrets, chaves JWT, senhas ou connection strings reais.

## Executando no Visual Studio

1. Abra [Extensionista_4.slnx](Extensionista_4/Extensionista_4.slnx).
2. Selecione o perfil `FullStack`.
3. Execute `Aplicativo.Api` com IIS Express.
4. Execute `Aplicativo.Mobile` no emulador Android ou em `Windows Machine`.

Com o IIS Express, a documentação Scalar fica disponível em:

```text
https://localhost:44380/scalar/v1
```

Quando a API for executada pelo perfil de projeto, a URL HTTPS padrão é:

```text
https://localhost:7240/scalar/v1
```

## Verificação rápida

- API inicia sem erro de configuração de JWT.
- Scalar abre e lista os endpoints.
- Login Google funciona no Android.
- Login Google funciona no Windows.
- O JWT é salvo no `SecureStorage`.
- O banco SQLite é criado na primeira utilização do `LocalDatabase`.
- Dados locais continuam acessíveis sem conexão.

## Estado atual

- Estrutura inicial da solução criada.
- Autenticação Google configurada para Android e Windows.
- API emitindo JWT próprio do LogTrack.
- Scalar configurado para documentação e testes.
- Infraestrutura inicial de SQLite e fila de sincronização criada.
- Persistência de usuários, RBAC completo, entidades de negócio, sincronização e migrations do SQL Server ainda devem ser implementados conforme as funcionalidades forem desenvolvidas.

## Documentos relacionados

- [Regras de arquitetura para agentes e desenvolvedores](AGENTS.md)
- [Documentação da autenticação](Extensionista_4/AUTHENTICATION.md)
- [Solução Visual Studio](Extensionista_4/Extensionista_4.slnx)
