using SQLite;

namespace Aplicativo.Mobile.Data.Local;

/// <summary>
/// Outbox local para alterações feitas sem conexão.
/// O processamento da fila será implementado pelo serviço de sincronização.
/// </summary>
[Table("__pending_sync_operations")]
public sealed class PendingSyncOperation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public int Attempts { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }

    public string? LastError { get; set; }
}
