using Aplicativo.Api.Options;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace Aplicativo.Api.Services;

public sealed class GoogleTokenService(IOptions<GoogleOptions> options)
{
    private readonly GoogleOptions _options = options.Value;

    public async Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken, string nonce, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            throw new ArgumentException("O id_token do Google é obrigatório.", nameof(idToken));

        if (_options.AllowedClientIds.Length == 0 ||
            _options.AllowedClientIds.Any(id => id.StartsWith("COLOQUE_AQUI", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Configure Google:AllowedClientIds antes de autenticar.");

        var payload = await GoogleJsonWebSignature.ValidateAsync(
            idToken,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = _options.AllowedClientIds,
                IssuedAtClockTolerance = TimeSpan.FromMinutes(1)
            });

        if (!payload.EmailVerified)
            throw new UnauthorizedAccessException("A conta Google não possui e-mail verificado.");

        if (string.IsNullOrWhiteSpace(nonce) || !string.Equals(payload.Nonce, nonce, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("O nonce do token Google não confere.");

        return payload;
    }
}
