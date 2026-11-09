namespace Aplicativo.Api.Options;

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string AdminEmail { get; init; } = string.Empty;
}
