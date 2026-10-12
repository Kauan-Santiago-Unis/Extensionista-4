using SQLite;
namespace Aplicativo.Mobile.Data.Local;

[Table("PendingSyncOperations")]
public sealed class PendingSyncOperation
{
    [PrimaryKey] public string Id { get; set; } = Guid.NewGuid().ToString();
    [Indexed] public string Scope { get; set; } = "demo";
    public string Kind { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int Attempts { get; set; }
    public DateTime NextAttemptUtc { get; set; } = DateTime.UtcNow;
    public string? LastError { get; set; }
}
