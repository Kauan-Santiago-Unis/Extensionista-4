namespace Aplicativo.Core.Models;

public sealed record Vehicle(string Plate, string Model, int Year, int Mileage, string Status)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Brand { get; init; } = "Não informado";
    public decimal CapacityTons { get; init; }
}
public sealed record Maintenance(string Plate, string Description, DateTime Date, decimal Cost, string Status)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string PaymentStatus { get; init; } = "Pendente";
    public string PaymentMethod { get; init; } = "Não informado";
    public DateTime? PaidAt { get; init; }
    public DateTime? DueDate { get; init; }
}
public sealed record Refueling(string Plate, string Fuel, decimal Liters, decimal Cost, DateTime Date)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? TripId { get; init; }
}
public sealed record Transaction(string Description, string Category, decimal Amount, DateTime Date)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? CategoryId { get; init; }
    public string PaymentMethod { get; init; } = "Não informado";
    public string Status { get; init; } = "Pago/Recebido";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? PaidAt { get; init; }
    public Guid? TripId { get; init; }
    public Guid? DebtId { get; init; }
    public string? ReceiptPath { get; init; }
    public string Source { get; init; } = "Manual";
    public DateTime ReferenceDate => Status == "Pendente" ? DueDate ?? Date : PaidAt ?? Date;
}
public sealed record FinancialCategory(Guid Id, string Title, string Type, bool Active = true);
public sealed record RoutePlan(Guid Id, string Origin, string Destination, decimal EstimatedKm)
{
    public override string ToString() => $"{Origin} → {Destination}";
}
public sealed record Trip(Guid Id, string Plate, Guid RouteId, string DriverId, int InitialMileage, DateTime StartedAt)
{
    public int? FinalMileage { get; init; }
    public DateTime? FinishedAt { get; init; }
    public string Status => FinalMileage is null ? "Em andamento" : "Concluída";
    public int Distance => FinalMileage is int end ? end - InitialMileage : 0;
}
public sealed record Debt(Guid Id, string Description, int Installments, decimal InstallmentAmount, DateTime FirstDueDate, Guid CategoryId, decimal? EarlySettlement);
public sealed record DemoUser(string Id, string Name, string Role, bool Approved);
public sealed record LocalOperation(Guid Id, string Kind, DateTime CreatedAt);

public sealed class FleetData
{
    public int SchemaVersion { get; set; }
    public string CompanyName { get; set; } = "Empresa";
    public string UserName { get; set; } = "Carlos Mendes";
    public List<Vehicle> Vehicles { get; set; } = [];
    public List<Maintenance> Maintenances { get; set; } = [];
    public List<Refueling> Refuelings { get; set; } = [];
    public List<Transaction> Transactions { get; set; } = [];
    public List<FinancialCategory> Categories { get; set; } = Defaults.Categories();
    public List<string> PaymentMethods { get; set; } = ["Boleto", "Pix", "Transferência TED", "Cartão Corporativo"];
    public List<RoutePlan> Routes { get; set; } = [];
    public List<Trip> Trips { get; set; } = [];
    public List<Debt> Debts { get; set; } = [];
    public List<DemoUser> Users { get; set; } = [new("admin", "Carlos Mendes", "Administrador", true), new("driver", "Motorista de demonstração", "Operador/Motorista", true), new("pending", "Usuário de exemplo", "Operador/Motorista", false)];
    public List<LocalOperation> LocalOperations { get; set; } = [];
}
public static class Defaults
{
    public static List<FinancialCategory> Categories() => [new(Guid.NewGuid(), "Frete", "Entrada"), new(Guid.NewGuid(), "Serviço", "Entrada"), new(Guid.NewGuid(), "Combustível", "Saída"), new(Guid.NewGuid(), "Pedágio", "Saída"), new(Guid.NewGuid(), "Salário", "Saída"), new(Guid.NewGuid(), "Manutenção", "Saída"), new(Guid.NewGuid(), "Financiamento", "Saída")];
}
