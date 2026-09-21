using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aplicativo.Mobile;

public sealed class GoogleAuthService
{
    private const string GoogleServerClientId = "296628721462-s3n3ntl7haeu01h53m5f0m2mn2bo3ajm.apps.googleusercontent.com";
    private const string ApiBaseUrl =
#if ANDROID
        // O perfil FullStack usa o IIS Express em https://localhost:44380.
        // Dentro do emulador Android, o computador host é acessado por 10.0.2.2.
        "https://10.0.2.2:44380";
#else
        "https://localhost:7240";
#endif
    private const string RedirectUri = "aplicativomobile://oauth2redirect";
    private const string GoogleClientId =
#if IOS || MACCATALYST
        "COLOQUE_AQUI_O_CLIENT_ID_IOS.apps.googleusercontent.com";
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
#else
        return await GetGoogleIdTokenWithWebAuthenticatorAsync(cancellationToken);
#endif
    }

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
