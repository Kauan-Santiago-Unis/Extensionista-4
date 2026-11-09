namespace Aplicativo.Mobile.Services;

// Presentation policy for the explicitly local demonstration, not server authorization.
public static class AccessPolicy
{
    public static readonly string[] Roles = ["Administrador", "Gestor de Frota", "Financeiro", "Operador/Motorista"];
    public static bool Can(string role, string action) => role == "Administrador" || action switch
    {
        "profile" => Roles.Contains(role),
        "finance" => role == "Financeiro",
        "fleet" or "trips" => role is "Gestor de Frota" or "Operador/Motorista",
        "manageFleet" or "logistics" => role == "Gestor de Frota",
        _ => false
    };
}
