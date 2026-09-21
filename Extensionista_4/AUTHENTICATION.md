# Google SSO — OAuth2/OIDC

O Android usa Credential Manager + Sign in with Google. iOS/Windows/MacCatalyst mantêm o fluxo OAuth2 com PKCE via `WebAuthenticator`. Em ambos os casos, a API valida o `id_token` e o `nonce` antes de emitir o JWT do aplicativo.

```text
Android -> Credential Manager / Google Play Services
        <- id_token + nonce
MAUI (demais plataformas) -> Google (navegador + PKCE)
        <- id_token + nonce
MAUI -> POST /api/auth/google
API  -> valida assinatura, issuer, audience, email_verified e nonce
     <- JWT próprio da aplicação
MAUI -> SecureStorage (sessão)
```

## 1. Criar as credenciais no Google Cloud

1. Crie ou selecione um projeto no [Google Cloud Console](https://console.cloud.google.com/).
2. Configure a tela de consentimento OAuth.
3. Crie um OAuth Client ID do tipo **Web application** para a API. Esse será o `serverClientId` do Credential Manager e a audiência do ID token.
4. Crie um OAuth Client ID do tipo Android, associado ao `ApplicationId` e ao SHA-1 da assinatura debug.
5. Para iOS/MacCatalyst, crie também um Client ID da plataforma, se for testar essas plataformas.
6. Copie o Client ID Web para `GoogleServerClientId` em `src/Aplicativo.Mobile/GoogleAuthService.cs`.
7. Coloque o Client ID Web em `Google:AllowedClientIds` na API. Adicione o Client ID iOS apenas se a plataforma correspondente também for usada.

O aplicativo mobile não deve possuir `client_secret`. No Android, o Credential Manager não usa callback customizado nem `WebAuthenticator`.

## 2. Configurar a API sem commitar segredo

Na pasta `src/Aplicativo.Api`, use User Secrets durante o desenvolvimento:

```powershell
dotnet user-secrets init
dotnet user-secrets set "Google:AllowedClientIds:0" "SEU_CLIENT_ID_WEB.apps.googleusercontent.com"
dotnet user-secrets set "Google:AllowedClientIds:1" "SEU_CLIENT_ID_IOS.apps.googleusercontent.com"
dotnet user-secrets set "Jwt:Key" "uma-chave-local-com-no-minimo-32-caracteres-e-fora-do-git"
```

O `appsettings.json` contém apenas placeholders para documentar a estrutura. Em produção, injete a chave por Secret Manager, variável de ambiente ou serviço de secrets.

## 3. Executar localmente pelo perfil FullStack

```powershell
dotnet user-secrets list --project .\src\Aplicativo.Api\Aplicativo.Api.csproj
dotnet build ..\Extensionista_4.slnx -f net10.0-android
```

No Visual Studio, selecione o perfil de inicialização `FullStack` e execute. Ele inicia `Aplicativo.Api` pelo IIS Express e `Aplicativo.Mobile` no emulador Android.

O Scalar fica disponível em `https://localhost:44380/scalar/v1`. No emulador, o MAUI acessa a mesma API por `https://10.0.2.2:44380`; essa URL já está configurada em `GoogleAuthService`. O código aceita o certificado local do IIS Express somente em builds `Debug` do Android.

Para um aparelho físico, substitua `10.0.2.2` pelo IP do computador na rede local e configure o IIS Express para aceitar conexões pela rede. Em produção, use HTTPS com um certificado válido.

## 4. Endpoints

### Login

`POST /api/auth/google`

```json
{
  "idToken": "ID_TOKEN_RETORNADO_PELO_GOOGLE",
  "nonce": "NONCE_GERADO_PELO_CREDENTIAL_MANAGER"
}
```

Resposta:

```json
{
  "accessToken": "JWT_DA_APLICACAO",
  "expiresAt": "2026-08-24T12:00:00Z",
  "user": {
    "id": "GOOGLE_SUB",
    "email": "usuario@example.com",
    "name": "Nome do usuário",
    "picture": "https://..."
  }
}
```

### Sessão atual

`GET /api/auth/me` com:

```text
Authorization: Bearer JWT_DA_APLICACAO
```

O usuário criado neste primeiro passo é representado pelas claims do JWT. A persistência em uma tabela `Usuarios` deve ser adicionada junto com o banco/EF Core.

## 5. Checklist de evidência para a apresentação

- Tela de login do MAUI.
- Tela de consentimento/seleção da conta Google.
- Tela do app com o nome/e-mail retornado.
- Fechar e abrir o app mostrando a sessão restaurada pelo `SecureStorage`.
- Requisição autenticada para `/api/auth/me`.
