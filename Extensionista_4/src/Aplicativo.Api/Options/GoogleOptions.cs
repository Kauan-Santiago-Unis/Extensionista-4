namespace Aplicativo.Api.Options;

public sealed class GoogleOptions
{
    public const string SectionName = "Google";

    public string[] AllowedClientIds { get; init; } = [];
}
