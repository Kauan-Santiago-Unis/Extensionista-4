using SQLite;
namespace Aplicativo.Mobile.Data.Local;

public sealed class LocalDatabase(string databasePath)
{
    private readonly SemaphoreSlim initialization = new(1, 1);
    private SQLiteAsyncConnection? connection;
    public string DatabasePath => databasePath;
    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (connection is not null) return connection;
        await initialization.WaitAsync();
        try
        {
            if (connection is not null) return connection;
            SQLitePCL.Batteries_V2.Init();
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            var candidate = new SQLiteAsyncConnection(databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
            await candidate.ExecuteScalarAsync<string>("PRAGMA journal_mode=WAL");
            await candidate.ExecuteScalarAsync<int>("PRAGMA busy_timeout=5000");
            await candidate.CreateTableAsync<LocalDatabaseMetadata>();
            await candidate.CreateTableAsync<LocalSnapshot>();
            await candidate.CreateTableAsync<PendingSyncOperation>();
            await candidate.InsertOrReplaceAsync(new LocalDatabaseMetadata { Key = "SchemaVersion", Value = "1" });
            connection = candidate;
            return connection;
        }
        finally { initialization.Release(); }
    }
    public async Task<string?> ReadAsync(string scope = "demo")
    {
        var db = await GetConnectionAsync();
        return (await db.FindAsync<LocalSnapshot>(scope))?.Payload;
    }
    public async Task SaveAsync(string payload, PendingSyncOperation? operation = null, string scope = "demo")
    {
        try
        {
            var db = await GetConnectionAsync();
            await db.RunInTransactionAsync(tx =>
            {
                tx.InsertOrReplace(new LocalSnapshot { Scope = scope, Payload = payload, UpdatedAtUtc = DateTime.UtcNow });
                if (operation is not null)
                {
                    if (operation.Scope != scope) throw new InvalidOperationException("Escopo da operação não confere.");
                    tx.Insert(operation); // Duplicate operation IDs roll back the snapshot as well.
                }
            });
        }
        catch (SQLiteException ex) { throw new IOException("Não foi possível gravar no banco local.", ex); }
    }
    public async Task<List<PendingSyncOperation>> PendingAsync(string scope)
    {
        var db = await GetConnectionAsync();
        return await db.Table<PendingSyncOperation>().Where(x => x.Scope == scope).OrderBy(x => x.CreatedAtUtc).ToListAsync();
    }
}

