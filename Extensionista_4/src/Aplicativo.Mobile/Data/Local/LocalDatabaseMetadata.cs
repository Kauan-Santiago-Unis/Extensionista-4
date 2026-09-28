using SQLite;

namespace Aplicativo.Mobile.Data.Local;

[Table("__local_database_metadata")]
public sealed class LocalDatabaseMetadata
{
    [PrimaryKey]
    public int Id { get; set; }

    public int SchemaVersion { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastSynchronizationAtUtc { get; set; }
}
