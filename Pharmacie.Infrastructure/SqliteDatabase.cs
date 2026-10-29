using Microsoft.Data.Sqlite;
using Pharmacie.Domain;
namespace Pharmacie.Infrastructure;
public sealed class SqliteConnectionFactory(string databasePath)
{ public SqliteConnection Create() { var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, Mode = SqliteOpenMode.ReadWriteCreate }.ToString()); c.Open(); using var p = c.CreateCommand(); p.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;"; p.ExecuteNonQuery(); return c; } }
public sealed class DatabaseInitializer(SqliteConnectionFactory factory) : IDatabaseInitializer
{ public async Task InitializeAsync(CancellationToken cancellationToken = default) { await using var c = factory.Create(); await using var cmd = c.CreateCommand(); cmd.CommandText = Schema.Sql; await cmd.ExecuteNonQueryAsync(cancellationToken); try { await using var alter = c.CreateCommand(); alter.CommandText = "ALTER TABLE StockLots ADD COLUMN IsBlocked INTEGER NOT NULL DEFAULT 0"; await alter.ExecuteNonQueryAsync(cancellationToken); } catch (SqliteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase)) { } } }
internal static class Schema
{
 public const string Sql = """
CREATE TABLE IF NOT EXISTS SchemaMigrations (Version INTEGER PRIMARY KEY, AppliedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS Roles (Id TEXT PRIMARY KEY, Name TEXT NOT NULL UNIQUE, CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS Users (Id TEXT PRIMARY KEY, Username TEXT NOT NULL UNIQUE, FullName TEXT NOT NULL, PasswordHash TEXT NOT NULL, RoleId TEXT NOT NULL REFERENCES Roles(Id), IsActive INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS Categories (Id TEXT PRIMARY KEY, Name TEXT NOT NULL UNIQUE, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS Products (Id TEXT PRIMARY KEY, Reference TEXT NOT NULL UNIQUE, Designation TEXT NOT NULL, CategoryId TEXT NOT NULL REFERENCES Categories(Id), Dosage TEXT, Forme TEXT, Unit TEXT NOT NULL, Barcode TEXT UNIQUE, SalePriceMinor INTEGER NOT NULL, MinimumStock REAL NOT NULL DEFAULT 0, IsActive INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS Suppliers (Id TEXT PRIMARY KEY, Name TEXT NOT NULL, Contact TEXT, Phone TEXT, Email TEXT, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS StockLots (Id TEXT PRIMARY KEY, ProductId TEXT NOT NULL REFERENCES Products(Id), LotNumber TEXT NOT NULL, ExpiryDate TEXT, Quantity REAL NOT NULL DEFAULT 0, PurchaseCostMinor INTEGER NOT NULL, IsBlocked INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, UNIQUE(ProductId, LotNumber));
CREATE TABLE IF NOT EXISTS StockReceipts (Id TEXT PRIMARY KEY, SupplierId TEXT REFERENCES Suppliers(Id), ReceiptNumber TEXT NOT NULL UNIQUE, ReceivedAt TEXT NOT NULL, UserId TEXT NOT NULL REFERENCES Users(Id), CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS StockReceiptLines (Id TEXT PRIMARY KEY, ReceiptId TEXT NOT NULL REFERENCES StockReceipts(Id), ProductId TEXT NOT NULL REFERENCES Products(Id), LotId TEXT NOT NULL REFERENCES StockLots(Id), Quantity REAL NOT NULL, UnitCostMinor INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS StockMovements (Id TEXT PRIMARY KEY, ProductId TEXT NOT NULL REFERENCES Products(Id), LotId TEXT REFERENCES StockLots(Id), Quantity REAL NOT NULL, MovementType TEXT NOT NULL, OccurredAt TEXT NOT NULL, UserId TEXT NOT NULL REFERENCES Users(Id), Reason TEXT, CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS Sales (Id TEXT PRIMARY KEY, SaleNumber TEXT NOT NULL UNIQUE, SoldAt TEXT NOT NULL, UserId TEXT NOT NULL REFERENCES Users(Id), SubtotalMinor INTEGER NOT NULL, DiscountMinor INTEGER NOT NULL DEFAULT 0, TotalMinor INTEGER NOT NULL, CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS SaleLines (Id TEXT PRIMARY KEY, SaleId TEXT NOT NULL REFERENCES Sales(Id), ProductId TEXT NOT NULL REFERENCES Products(Id), LotId TEXT REFERENCES StockLots(Id), Quantity REAL NOT NULL, UnitPriceMinor INTEGER NOT NULL, UnitCostMinor INTEGER NOT NULL, DiscountMinor INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS SalePayments (Id TEXT PRIMARY KEY, SaleId TEXT NOT NULL REFERENCES Sales(Id), Method TEXT NOT NULL, AmountMinor INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS Returns (Id TEXT PRIMARY KEY, ReturnNumber TEXT NOT NULL UNIQUE, SaleId TEXT REFERENCES Sales(Id), ReturnedAt TEXT NOT NULL, UserId TEXT NOT NULL REFERENCES Users(Id), Reason TEXT NOT NULL, CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS ReturnLines (Id TEXT PRIMARY KEY, ReturnId TEXT NOT NULL REFERENCES Returns(Id), ProductId TEXT NOT NULL REFERENCES Products(Id), LotId TEXT REFERENCES StockLots(Id), Quantity REAL NOT NULL, UnitPriceMinor INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS CashSessions (Id TEXT PRIMARY KEY, OpenedAt TEXT NOT NULL, ClosedAt TEXT, OpeningBalanceMinor INTEGER NOT NULL, ClosingBalanceMinor INTEGER, UserId TEXT NOT NULL REFERENCES Users(Id));
CREATE TABLE IF NOT EXISTS CashMovements (Id TEXT PRIMARY KEY, CashSessionId TEXT NOT NULL REFERENCES CashSessions(Id), Type TEXT NOT NULL, AmountMinor INTEGER NOT NULL, Reason TEXT NOT NULL, OccurredAt TEXT NOT NULL, UserId TEXT NOT NULL REFERENCES Users(Id));
CREATE TABLE IF NOT EXISTS InventorySessions (Id TEXT PRIMARY KEY, StartedAt TEXT NOT NULL, ClosedAt TEXT, UserId TEXT NOT NULL REFERENCES Users(Id), Status TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS InventoryLines (Id TEXT PRIMARY KEY, InventorySessionId TEXT NOT NULL REFERENCES InventorySessions(Id), ProductId TEXT NOT NULL REFERENCES Products(Id), LotId TEXT REFERENCES StockLots(Id), ExpectedQuantity REAL NOT NULL, CountedQuantity REAL NOT NULL, Difference REAL NOT NULL);
CREATE TABLE IF NOT EXISTS AuditLogs (Id TEXT PRIMARY KEY, UserId TEXT REFERENCES Users(Id), Action TEXT NOT NULL, EntityName TEXT NOT NULL, EntityId TEXT, Details TEXT, CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS AppSettings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_Products_CategoryId ON Products(CategoryId); CREATE INDEX IF NOT EXISTS IX_StockLots_ProductExpiry ON StockLots(ProductId, ExpiryDate); CREATE INDEX IF NOT EXISTS IX_StockMovements_ProductDate ON StockMovements(ProductId, OccurredAt); CREATE INDEX IF NOT EXISTS IX_Sales_SoldAt ON Sales(SoldAt);
INSERT OR IGNORE INTO SchemaMigrations(Version, AppliedAt) VALUES (1, datetime('now'));
""";
}
