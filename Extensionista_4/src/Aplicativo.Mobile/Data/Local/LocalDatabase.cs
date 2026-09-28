using SQLite;

namespace Aplicativo.Mobile.Data.Local;

/// <summary>
/// Ponto único de acesso ao banco local do aplicativo.
/// A conexão é criada em FileSystem.AppDataDirectory e as tabelas de
/// infraestrutura são criadas na primeira utilização.
/// </summary>
public sealed class LocalDatabase
{
    public const string DatabaseFilename = "logtrack.sqlite";

    private readonly SQLiteAsyncConnection _connection;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public LocalDatabase()
    {
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
        _connection = new SQLiteAsyncConnection(
            databasePath,
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.SharedCache);
    }

    public async Task<SQLiteAsyncConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        return _connection;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
                return;

            await _connection.CreateTableAsync<LocalDatabaseMetadata>();
            await _connection.CreateTableAsync<PendingSyncOperation>();

            var metadata = await _connection.Table<LocalDatabaseMetadata>()
                .Where(item => item.Id == 1)
                .FirstOrDefaultAsync();

            if (metadata is null)
            {
                await _connection.InsertAsync(new LocalDatabaseMetadata
                {
                    Id = 1,
                    SchemaVersion = 1,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
