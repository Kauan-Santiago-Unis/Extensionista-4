using Aplicativo.Mobile.Data.Local;
using Aplicativo.Mobile.Services.Sync;
using System.Text.Json;
using Aplicativo.Core.Models;
using Aplicativo.Mobile.Services;

int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS: " + name); }
async Task Reject(Func<Task> action, string name) { try { await action(); } catch (ArgumentException) { Check(true, name); return; } throw new Exception("Expected rejection: " + name); }
var store = new DemoStore(); await store.LoadAsync();
Check(store.Data.Vehicles.Count == 4, "Initial fleet");
Check(store.Balance == 1952.50m, "Balance counts paid records only");
Check(store.Forecast(DateTime.Today.AddMonths(2)) == 3872.50m, "Forecast includes dated pending values");
var initialBalance = store.Balance;
var pending = store.Data.Maintenances[0];
await store.CommitAsync(d => d.Maintenances[0] = pending with { Status = "Concluída" }, "test");
Check(store.Balance == initialBalance, "Completing service does not imply payment");
await store.CommitAsync(d => d.Maintenances[0] = d.Maintenances[0] with { PaymentStatus = "Pago/Recebido", PaidAt = DateTime.Today }, "test");
Check(store.Balance == initialBalance - pending.Cost, "Paying maintenance affects cash once");
var trip = new Trip(Guid.NewGuid(), "ABC-1D23", store.Data.Routes[0].Id, "driver", 45230, DateTime.Now);
await store.CommitAsync(d => DemoStore.AddTrip(d, trip), "trip-start");
Check(store.Data.Vehicles[0].Status == "Em viagem", "Opening a trip reserves vehicle");
await Reject(() => store.CommitAsync(d => DemoStore.AddTrip(d, trip with { Id = Guid.NewGuid() }), "duplicate"), "Cannot start second active trip");
await Reject(() => store.CommitAsync(d => DemoStore.FinishTrip(d, trip.Id, 45230), "invalid"), "Final odometer must be strictly greater");
await Reject(() => store.CommitAsync(d => DemoStore.FinishTrip(d, trip.Id, 45000), "invalid"), "Odometer cannot regress");
await store.CommitAsync(d => DemoStore.FinishTrip(d, trip.Id, 45330), "trip-finish");
Check(store.Data.Trips[0].Distance == 100 && store.Data.Vehicles[0].Mileage == 45330, "Closing updates distance and odometer");
Check(store.Data.Vehicles[0].Status == "Disponível", "Completed trip releases vehicle");
await Reject(() => store.CommitAsync(d => DemoStore.FinishTrip(d, trip.Id, 45430), "invalid"), "Completed trip cannot close twice");
var category = store.Data.Categories.First(c => c.Title == "Financiamento");
var debt = new Debt(Guid.NewGuid(), "Financiamento", 3, 100, new DateTime(2027, 1, 31), category.Id, null);
var beforeDebtBalance = store.Balance;
await store.CommitAsync(d => DemoStore.AddDebt(d, debt, "Boleto"), "debt");
var installments = store.Data.Transactions.Where(t => t.DebtId == debt.Id).ToList();
Check(installments.Count == 3 && installments.Sum(t => t.Amount) == -300, "Debt creates all installments");
Check(installments[1].DueDate == new DateTime(2027, 2, 28) && installments[2].DueDate == new DateTime(2027, 3, 31), "Month-end installments do not drift");
Check(store.Balance == beforeDebtBalance && installments.All(t => t.Status == "Pendente"), "Pending installments do not affect current cash");
var before = JsonSerializer.Serialize(store.Data);
await Reject(() => store.CommitAsync(d => { DemoStore.AddDebt(d, debt with { Id = Guid.NewGuid() }, "Pix"); throw new ArgumentException("Simulated partial failure"); }, "rollback"), "Partial debt failure rejected");
Check(JsonSerializer.Serialize(store.Data) == before, "Rollback restores all collections and operation log");
var reload = new DemoStore(); await reload.LoadAsync();
Check(reload.Data.Trips[0].Id == trip.Id && reload.Balance == store.Balance, "IDs, journeys and balances persist");
var snapshot = JsonSerializer.Serialize(store.Data);
var db = new LocalDatabase(Path.Combine(FileSystem.AppDataDirectory, "logtrack.db3"));
var pendingRows = await db.PendingAsync("demo");
Check(pendingRows.Count == store.Data.LocalOperations.Count, "Each committed change has an outbox row");
try { await db.SaveAsync("invalid replacement", pendingRows[0]); throw new Exception("Expected duplicate rejection"); }
catch (IOException) { Check(await db.ReadAsync() == snapshot, "Snapshot and outbox roll back together"); }
var connection = await db.GetConnectionAsync();
await connection.ExecuteAsync("CREATE TRIGGER fail_snapshot BEFORE INSERT ON LocalSnapshots BEGIN SELECT RAISE(ABORT, 'simulated disk failure'); END");
try { await store.CommitAsync(d => d.CompanyName = "Should roll back", "io-failure"); throw new Exception("Expected write failure"); }
catch (IOException) { Check(JsonSerializer.Serialize(store.Data) == snapshot, "SQLite failure rolls back memory"); }
await connection.ExecuteAsync("DROP TRIGGER fail_snapshot");
Check(!AccessPolicy.Can("Operador/Motorista", "finance") && !AccessPolicy.Can("Operador/Motorista", "admin"), "Driver blocked from finance and admin");
Check(AccessPolicy.Can("Operador/Motorista", "trips") && !AccessPolicy.Can("Gestor de Frota", "finance"), "Operational role permissions");
Check(AccessPolicy.Can("Financeiro", "finance") && !AccessPolicy.Can("Financeiro", "manageFleet"), "Finance role isolated");
Check(!AccessPolicy.Can("Unknown", "profile"), "Unknown role fails closed");
var csv = DemoStore.Csv([new("=SUM(A1)", "Frete", -25.50m, DateTime.Today)]);
Check(csv.Contains("\"'=SUM(A1)\"") && csv.Contains("-25,50") && csv.Contains("Status"), "CSV escapes spreadsheet formulas and formats money");
await File.WriteAllTextAsync(Path.Combine(FileSystem.AppDataDirectory, "fleet-demo.json"), "{invalid");
var recovered = new DemoStore(new LocalDatabase(Path.Combine(FileSystem.AppDataDirectory, "recovery.db3"))); await recovered.LoadAsync();
Check(recovered.LoadWarning is not null && recovered.Data.Vehicles.Count == 4, "Corrupt legacy storage gives visible warning and recovery copy");
await recovered.SaveAsync();
Check(Directory.GetFiles(FileSystem.AppDataDirectory, "*.recovery-*").Length == 1, "Original corrupt data preserved before replacement");
await File.WriteAllTextAsync(Path.Combine(FileSystem.AppDataDirectory, "fleet-demo.json"), "{\"Vehicles\":[{\"Plate\":\"ABC-1234\",\"Model\":\"Teste\",\"Year\":2020,\"Mileage\":10,\"Status\":\"Ativo\"}],\"Maintenances\":[],\"Refuelings\":[],\"Transactions\":[]}");
var legacyDb = new LocalDatabase(Path.Combine(FileSystem.AppDataDirectory, "legacy.db3"));
var legacy = new DemoStore(legacyDb); await legacy.LoadAsync();
Check(legacy.Data.Vehicles[0].Status == "Disponível" && legacy.Data.PaymentMethods.Contains("Pix"), "Legacy records migrate with defaults");
Check(File.Exists(Path.Combine(FileSystem.AppDataDirectory, "fleet-demo.json")), "Legacy source retained after import");
await File.WriteAllTextAsync(Path.Combine(FileSystem.AppDataDirectory, "fleet-demo.json"), "invalid now");
await legacy.LoadAsync();
Check(legacy.Data.Vehicles[0].Plate == "ABC-1234", "SQLite takes precedence over legacy JSON");
var sends = 0;
var worker = new OutboxProcessor(db);
await worker.DrainAsync("demo", (_, _) => { sends++; return Task.FromResult(true); });
Check(sends == 0, "Demo data never sent by sync processor");
var op = new PendingSyncOperation { Scope = "user:test", Kind = "test", Payload = "{}" };
await db.SaveAsync("{}", op, op.Scope);
await worker.DrainAsync(op.Scope, (_, _) => { sends++; return Task.FromResult(false); });
var retry = (await db.PendingAsync(op.Scope)).Single();
Check(retry.Attempts == 1 && retry.NextAttemptUtc > DateTime.UtcNow, "Failed sync retained with retry delay");
retry.NextAttemptUtc = DateTime.UtcNow.AddSeconds(-1); await connection.UpdateAsync(retry);
await worker.DrainAsync(op.Scope, (_, _) => Task.FromResult(true));
Check((await db.PendingAsync(op.Scope)).Count == 0, "Only acknowledged operations removed");
Console.WriteLine($"{checks} checks passed.");

internal static class FileSystem
{
    public static string AppDataDirectory { get; } = Create();
    private static string Create() { var path = Path.Combine(Path.GetTempPath(), "fleet-checks-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
}
