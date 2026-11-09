namespace Aplicativo.Api.Services;

public sealed record ModuleSeed(string Code, string Name, string Description);

public static class AuthorizationCatalog
{
    public const string AdministratorGroupName = "Administrador";

    // Catálogo inicial explícito. Novos módulos devem ser adicionados por uma decisão própria,
    // e não liberados automaticamente para todos os grupos.
    public static IReadOnlyList<ModuleSeed> InitialModules { get; } =
    [
        new("DASHBOARD", "Dashboard", "Indicadores e visão geral"),
        new("FROTA", "Frota", "Veículos e dados operacionais"),
        new("MANUTENCAO", "Manutenção", "Manutenções da frota"),
        new("VIAGENS", "Viagens", "Abertura e acompanhamento de viagens"),
        new("ROTAS", "Rotas", "Rotas planejadas"),
        new("FINANCEIRO", "Financeiro", "Receitas, despesas e indicadores financeiros"),
        new("CATEGORIAS_FINANCEIRAS", "Categorias financeiras", "Categorias de lançamentos"),
        new("FORMAS_PAGAMENTO", "Formas de pagamento", "Formas de pagamento cadastradas"),
        new("USUARIOS", "Usuários", "Contas e aprovações de usuários"),
        new("ADMINISTRACAO", "Administração", "Configurações administrativas")
    ];

    public static IReadOnlySet<string> AdministratorModuleCodes { get; } =
        InitialModules.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
