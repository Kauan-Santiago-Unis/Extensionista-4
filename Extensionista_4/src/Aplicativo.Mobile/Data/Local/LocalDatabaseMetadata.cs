using SQLite;
namespace Aplicativo.Mobile.Data.Local;

[Table("LocalDatabaseMetadata")]
public sealed class LocalDatabaseMetadata
{
    [PrimaryKey] public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
[Table("LocalSnapshots")]
public sealed class LocalSnapshot
{
    [PrimaryKey] public string Scope { get; set; } = "demo";
    public string Payload { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; }
}
