using System.Net.Http.Json;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aplicativo.Mobile;

public sealed class GoogleAuthService
{
    private const string GoogleServerClientId = "296628721462-s3n3ntl7haeu01h53m5f0m2mn2bo3ajm.apps.googleusercontent.com";
    private const string GoogleDesktopClientId = "296628721462-mjapqe6j3mqgilhqg7hi77p4p3jk3eij.apps.googleusercontent.com";

    // O Google exige este valor na troca manual do authorization code pelo token.
    // Não compartilhe este segredo; mantenha-o apenas no ambiente local.
    private const string GoogleDesktopClientSecret = "COLOQUE_AQUI_O_CLIENT_SECRET_DESKTOP";
    private const string ApiBaseUrl =
#if ANDROID
        // O perfil FullStack usa o IIS Express em https://localhost:44380.
        // Dentro do emulador Android, o computador host é acessado por 10.0.2.2.
        "https://10.0.2.2:44380";
#elif WINDOWS
        // O perfil FullStack executa a API localmente no IIS Express.
        "https://localhost:44380";
#else
        "https://localhost:7240";
#endif
    private const string RedirectUri = "aplicativomobile://oauth2redirect";
    private const string GoogleClientId =
#if IOS || MACCATALYST
        "COLOQUE_AQUI_O_CLIENT_ID_IOS.apps.googleusercontent.com";
#elif WINDOWS
        GoogleDesktopClientId;
#else
        GoogleServerClientId;
#endif
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string SessionKey = "app_session";
    private readonly HttpClient _httpClient = CreateHttpClient();

    public async Task<AppSession> SignInAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var googleToken = await GetGoogleIdTokenAsync(cancellationToken);
        using var apiRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/auth/google")
        {
            Content = JsonContent.Create(new { idToken = googleToken.IdToken, nonce = googleToken.Nonce })
        };
#if ANDROID && DEBUG
        // O emulador chega ao computador por 10.0.2.2, mas o IIS Express
        // está vinculado ao hostname localhost. Mantemos a URL com 10.0.2.2
        // para alcançar o host e informamos ao IIS Express o hostname aceito.
        apiRequest.Headers.Host = "localhost:44380";
#endif
        using var apiResponse = await _httpClient.SendAsync(apiRequest, cancellationToken);
        var body = await apiResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!apiResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"A API recusou o login ({(int)apiResponse.StatusCode}): {body}");
        var session = JsonSerializer.Deserialize<AppSession>(body) ?? throw new InvalidOperationException("Resposta de login inválida.");
        await SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(session));
        return session;
    }

    public async Task<AppSession?> GetStoredSessionAsync()
    {
        var json = await SecureStorage.Default.GetAsync(SessionKey);
        var session = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<AppSession>(json);
        if (session is null || session.ExpiresAt <= DateTime.UtcNow) { await SignOutAsync(); return null; }
        return session;
    }

    public Task SignOutAsync() { SecureStorage.Default.Remove(SessionKey); return Task.CompletedTask; }

    private static async Task<GoogleIdTokenResult> GetGoogleIdTokenAsync(CancellationToken cancellationToken)
    {
#if ANDROID
        return await AndroidGoogleCredentialManager.GetIdTokenAsync(GoogleServerClientId, cancellationToken);
#elif WINDOWS
        return await GetGoogleIdTokenWithLoopbackAsync(cancellationToken);
#else
        return await GetGoogleIdTokenWithWebAuthenticatorAsync(cancellationToken);
#endif
    }

#if WINDOWS
    private static async Task<GoogleIdTokenResult> GetGoogleIdTokenWithLoopbackAsync(CancellationToken cancellationToken)
    {
        EnsureDesktopClientConfigured();

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var redirectUri = $"http://127.0.0.1:{endpoint.Port}/oauth2redirect/";
        var state = CreateRandomValue(32);
        var codeVerifier = CreateRandomValue(64);
        var nonce = CreateRandomValue(32);
        var query = new Dictionary<string, string>
        {
            ["client_id"] = GoogleClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["access_type"] = "offline",
            ["prompt"] = "select_account",
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = CreateCodeChallenge(codeVerifier),
            ["code_challenge_method"] = "S256"
        };

        using var cancellationRegistration = cancellationToken.Register(() => listener.Stop());
        var browserOpened = await Browser.Default.OpenAsync(
            new Uri($"{AuthEndpoint}?{ToQueryString(query)}"),
            BrowserLaunchMode.SystemPreferred);

        if (!browserOpened)
            throw new InvalidOperationException("Não foi possível abrir o navegador padrão para o login Google.");

        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        var callback = await ReadLoopbackCallbackAsync(client, cancellationToken);

        if (callback.TryGetValue("error", out var googleError))
            throw new InvalidOperationException($"O Google recusou o login: {googleError}.");

        if (!callback.TryGetValue("state", out var returnedState) || returnedState != state)
            throw new InvalidOperationException("O estado do OAuth2 não confere.");

        if (!callback.TryGetValue("code", out var code))
            throw new InvalidOperationException("O Google não retornou um authorization code.");

        using var response = await new HttpClient().PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = GoogleClientId,
            ["client_secret"] = GoogleDesktopClientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier
        }), cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"O Google não trocou o authorization code ({(int)response.StatusCode}): {responseBody}");

        var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(responseBody);
        return tokenResponse?.IdToken is { Length: > 0 }
            ? new GoogleIdTokenResult(tokenResponse.IdToken, nonce)
            : throw new InvalidOperationException("O Google não retornou id_token.");
    }

    private static async Task<Dictionary<string, string>> ReadLoopbackCallbackAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken)
            ?? throw new InvalidOperationException("O retorno OAuth2 do Google veio vazio.");

        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
        {
        }

        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new InvalidOperationException("O retorno OAuth2 do Google possui formato inválido.");

        var query = ParseQueryString(parts[1]);
        var html = "<html><head><meta charset='utf-8'><title>LogTrack</title></head>" +
                   "<body><h2>Login concluído</h2><p>Você pode voltar ao LogTrack.</p></body></html>";
        var body = Encoding.UTF8.GetBytes(html);
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        return query;
    }

    private static Dictionary<string, string> ParseQueryString(string requestTarget)
    {
        var query = requestTarget.Contains('?') ? requestTarget[(requestTarget.IndexOf('?') + 1)..] : string.Empty;
        return query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')),
                StringComparer.Ordinal);
    }
#endif

#if !ANDROID
    private static async Task<GoogleIdTokenResult> GetGoogleIdTokenWithWebAuthenticatorAsync(CancellationToken cancellationToken)
    {
        var state = CreateRandomValue(32);
        var codeVerifier = CreateRandomValue(64);
        var nonce = CreateRandomValue(32);
        var query = new Dictionary<string, string>
        {
            ["client_id"] = GoogleClientId, ["redirect_uri"] = RedirectUri,
            ["response_type"] = "code", ["scope"] = "openid email profile",
            ["access_type"] = "offline", ["prompt"] = "select_account",
            ["state"] = state, ["nonce"] = nonce,
            ["code_challenge"] = CreateCodeChallenge(codeVerifier), ["code_challenge_method"] = "S256"
        };
        var result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions { Url = new Uri($"{AuthEndpoint}?{ToQueryString(query)}"), CallbackUrl = new Uri(RedirectUri) }, cancellationToken);
        if (!result.Properties.TryGetValue("state", out var returnedState) || returnedState != state) throw new InvalidOperationException("O estado do OAuth2 não confere.");
        if (!result.Properties.TryGetValue("code", out var code)) throw new InvalidOperationException("O Google não retornou um authorization code.");
        using var response = await new HttpClient().PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code, ["client_id"] = GoogleClientId, ["redirect_uri"] = RedirectUri,
            ["grant_type"] = "authorization_code", ["code_verifier"] = codeVerifier
        }), cancellationToken);
        response.EnsureSuccessStatusCode();
        var tokenResponse = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken);
        return tokenResponse?.IdToken is { Length: > 0 } ? new GoogleIdTokenResult(tokenResponse.IdToken, nonce) : throw new InvalidOperationException("O Google não retornou id_token.");
    }
#endif

    private static string CreateRandomValue(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));
    private static string CreateCodeChallenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string ToQueryString(Dictionary<string, string> values) => string.Join("&", values.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
    private static void EnsureConfigured() { if (GoogleServerClientId.StartsWith("COLOQUE_AQUI", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Configure o Client ID Web no GoogleAuthService."); }

    private static void EnsureDesktopClientConfigured()
    {
        if (GoogleDesktopClientId.StartsWith("COLOQUE_AQUI", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Configure o Client ID OAuth do tipo Aplicativo para computador no GoogleAuthService.");
        if (string.IsNullOrWhiteSpace(GoogleDesktopClientSecret) ||
            GoogleDesktopClientSecret.StartsWith("COLOQUE_AQUI", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Configure o Client Secret OAuth do tipo Aplicativo para computador no GoogleAuthService.");
    }

    private static HttpClient CreateHttpClient()
    {
#if ANDROID && DEBUG
        // O certificado de desenvolvimento do IIS Express é emitido para
        // localhost, enquanto o emulador acessa o host por 10.0.2.2.
        // Esta exceção existe somente para o teste local em Debug.
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, _) =>
                request?.RequestUri?.Host == "10.0.2.2"
        };
        return new HttpClient(handler);
#elif WINDOWS && DEBUG
        // O certificado de desenvolvimento do IIS Express é local e só deve
        // ser aceito para o host local durante os testes de desenvolvimento.
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, _) =>
                request?.RequestUri?.Host == "localhost"
        };
        return new HttpClient(handler);
#else
        return new HttpClient();
#endif
    }
}

public sealed record GoogleIdTokenResult(string IdToken, string Nonce);
public sealed record GoogleTokenResponse([property: JsonPropertyName("id_token")] string IdToken);
public sealed record AppSession([property: JsonPropertyName("accessToken")] string AccessToken, [property: JsonPropertyName("expiresAt")] DateTime ExpiresAt, [property: JsonPropertyName("user")] AppUser User)
{
    public string Email => User.Email;
    public string Name => User.Name;
}
public sealed record AppUser([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("email")] string Email, [property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("picture")] string Picture);
