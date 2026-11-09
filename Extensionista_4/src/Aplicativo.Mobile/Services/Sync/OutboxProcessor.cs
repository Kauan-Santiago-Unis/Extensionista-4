using Aplicativo.Mobile.Data.Local;
namespace Aplicativo.Mobile.Services.Sync;

// Transport must authenticate at send time and return true only for a matching server acknowledgement.
// Demo data is deliberately excluded. Wire this to real tenant-scoped repositories when the API exists.
public sealed class OutboxProcessor(LocalDatabase database)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task DrainAsync(string scope, Func<PendingSyncOperation, CancellationToken, Task<bool>> send, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope == "demo") return;
        await gate.WaitAsync(cancellationToken);
        try
        {
            var db = await database.GetConnectionAsync();
            foreach (var operation in await database.PendingAsync(scope))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (operation.NextAttemptUtc > DateTime.UtcNow) break; // Preserve mutation order.
                bool accepted;
                try { accepted = await send(operation, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (HttpRequestException) { accepted = false; }
                catch (TaskCanceledException) { accepted = false; }
                if (accepted) { await db.DeleteAsync(operation); continue; }
                operation.Attempts++;
                operation.NextAttemptUtc = DateTime.UtcNow.AddSeconds(Math.Min(3600, 5 * Math.Pow(2, Math.Min(operation.Attempts, 10))));
                operation.LastError = "Sem confirmação do servidor"; // Never persist responses or credentials.
                await db.UpdateAsync(operation);
                break;
            }
        }
        finally { gate.Release(); }
    }
}
