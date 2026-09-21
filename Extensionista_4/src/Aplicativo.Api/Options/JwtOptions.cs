namespace Aplicativo.Api.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "Aplicativo.Api";
    public string Audience { get; init; } = "Aplicativo.Mobile";
    public string Key { get; init; } = string.Empty;
    public int ExpirationMinutes { get; init; } = 60;
}
