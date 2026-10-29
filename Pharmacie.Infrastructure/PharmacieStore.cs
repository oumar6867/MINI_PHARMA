using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Pharmacie.Domain;

namespace Pharmacie.Infrastructure;

public sealed record ProductRow(Guid Id, string Reference, string Designation, string Category, string Unit, decimal SalePrice, decimal MinimumStock, bool Active);
public sealed record StockRow(Guid LotId, Guid ProductId, string Reference, string Designation, string LotNumber, DateOnly? ExpiryDate, decimal Quantity, decimal PurchaseCost, bool Blocked);
public sealed record SaleCartLine(Guid ProductId, string Designation, decimal Quantity, decimal UnitPrice);
public sealed record SaleResult(string Number, decimal Total, decimal Change);

public sealed class PharmacieStore(SqliteConnectionFactory factory)
{
    public async Task SeedAsync()
    {
        await using var c = factory.Create();
        await using var tx = c.BeginTransaction();
        var roles = new[] { ("ADMIN", "Administrateur"), ("RESP", "Responsable"), ("CASH", "Vendeur/Caissier") };
        foreach (var (name, _) in roles)
        {
            await Exec(c, tx, "INSERT OR IGNORE INTO Roles(Id,Name,CreatedAt) VALUES($id,$name,$now)", ("$id", Guid.NewGuid().ToString()), ("$name", name), ("$now", DateTimeOffset.UtcNow.ToString("O")));
        }
        var adminRole = (await Scalar(c, tx, "SELECT Id FROM Roles WHERE Name='ADMIN'"))?.ToString() ?? throw new InvalidOperationException("Le rôle administrateur n'a pas pu être initialisé.");
        var adminExists = await Scalar(c, tx, "SELECT COUNT(*) FROM Users WHERE Username='admin'");
        if (Convert.ToInt32(adminExists) == 0)
        {
            var hash = PasswordHash.Create("admin");
            await Exec(c, tx, "INSERT INTO Users(Id,Username,FullName,PasswordHash,RoleId,CreatedAt,UpdatedAt) VALUES($id,'admin','Administrateur', $hash,$role,$now,$now)", ("$id", Guid.NewGuid().ToString()), ("$hash", hash), ("$role", adminRole), ("$now", DateTimeOffset.UtcNow.ToString("O")));
        }
        await Exec(c, tx, "INSERT OR IGNORE INTO Categories(Id,Name,CreatedAt,UpdatedAt) VALUES($id,'Médicaments',$now,$now)", ("$id", Guid.NewGuid().ToString()), ("$now", DateTimeOffset.UtcNow.ToString("O")));
        await tx.CommitAsync();
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        await using var c = factory.Create(); await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT u.Id,u.Username,u.FullName,r.Name,u.PasswordHash FROM Users u JOIN Roles r ON r.Id=u.RoleId WHERE u.Username=$u AND u.IsActive=1";
        cmd.Parameters.AddWithValue("$u", username.Trim()); await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync() || !PasswordHash.Verify(password, r.GetString(4))) return null;
        return new User(Guid.Parse(r.GetString(0)), r.GetString(1), r.GetString(2), r.GetString(3));
    }

    public async Task<IReadOnlyList<ProductRow>> GetProductsAsync(string? search = null)
    {
        await using var c = factory.Create(); await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT p.Id,p.Reference,p.Designation,IFNULL(c.Name,''),p.Unit,p.SalePriceMinor,p.MinimumStock,p.IsActive FROM Products p LEFT JOIN Categories c ON c.Id=p.CategoryId WHERE ($s='' OR p.Reference LIKE $like OR p.Designation LIKE $like OR IFNULL(p.Barcode,'') LIKE $like) ORDER BY p.Designation";
        var s = search?.Trim() ?? ""; cmd.Parameters.AddWithValue("$s", s); cmd.Parameters.AddWithValue("$like", $"%{s}%");
        var list = new List<ProductRow>(); await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new ProductRow(Guid.Parse(r.GetString(0)), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), Money.FromMinorUnits(r.GetInt64(5)), r.GetDecimal(6), r.GetInt64(7) == 1));
        return list;
    }

    public async Task<IReadOnlyList<StockRow>> GetStockAsync()
    {
        await using var c = factory.Create(); await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT l.Id,p.Id,p.Reference,p.Designation,l.LotNumber,l.ExpiryDate,l.Quantity,l.PurchaseCostMinor,l.IsBlocked FROM StockLots l JOIN Products p ON p.Id=l.ProductId ORDER BY p.Designation,l.ExpiryDate";
        var list = new List<StockRow>(); await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new StockRow(Guid.Parse(r.GetString(0)), Guid.Parse(r.GetString(1)), r.GetString(2), r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : DateOnly.Parse(r.GetString(5)), r.GetDecimal(6), Money.FromMinorUnits(r.GetInt64(7)), r.GetInt64(8) == 1));
        return list;
    }

    public async Task AddProductAsync(string reference, string designation, string category, string unit, decimal price, decimal minimum)
    {
        if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(designation)) throw new DomainException("La référence et la désignation sont obligatoires.");
        await using var c = factory.Create(); await using var tx = c.BeginTransaction();
        var categoryId = await Scalar(c, tx, "SELECT Id FROM Categories WHERE Name=$n", ("$n", category));
        if (categoryId is null) { categoryId = Guid.NewGuid().ToString(); await Exec(c, tx, "INSERT INTO Categories(Id,Name,CreatedAt,UpdatedAt) VALUES($id,$n,$now,$now)", ("$id", categoryId), ("$n", category), ("$now", DateTimeOffset.UtcNow.ToString("O"))); }
        await Exec(c, tx, "INSERT INTO Products(Id,Reference,Designation,CategoryId,Unit,SalePriceMinor,MinimumStock,CreatedAt,UpdatedAt) VALUES($id,$r,$d,$c,$u,$p,$m,$now,$now)", ("$id", Guid.NewGuid().ToString()), ("$r", reference.Trim()), ("$d", designation.Trim()), ("$c", categoryId), ("$u", unit), ("$p", Money.ToMinorUnits(price)), ("$m", minimum), ("$now", DateTimeOffset.UtcNow.ToString("O")));
        await tx.CommitAsync();
    }

    public async Task AddReceiptAsync(Guid productId, string lotNumber, DateOnly? expiry, decimal quantity, decimal cost, User user)
    {
        if (quantity <= 0) throw new DomainException("La quantité doit être supérieure à zéro.");
        await using var c = factory.Create(); await using var tx = c.BeginTransaction(); var now = DateTimeOffset.UtcNow.ToString("O");
        var lot = await Scalar(c, tx, "SELECT Id FROM StockLots WHERE ProductId=$p AND LotNumber=$l", ("$p", productId.ToString()), ("$l", lotNumber));
        string lotId = lot?.ToString() ?? Guid.NewGuid().ToString();
        if (lot is null) await Exec(c, tx, "INSERT INTO StockLots(Id,ProductId,LotNumber,ExpiryDate,Quantity,PurchaseCostMinor,CreatedAt,UpdatedAt) VALUES($id,$p,$l,$e,$q,$cost,$now,$now)", ("$id", lotId), ("$p", productId.ToString()), ("$l", lotNumber), ("$e", expiry?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value), ("$q", quantity), ("$cost", Money.ToMinorUnits(cost)), ("$now", now));
        else await Exec(c, tx, "UPDATE StockLots SET Quantity=Quantity+$q,PurchaseCostMinor=$cost,UpdatedAt=$now WHERE Id=$id", ("$q", quantity), ("$cost", Money.ToMinorUnits(cost)), ("$now", now), ("$id", lotId));
        await Exec(c, tx, "INSERT INTO StockMovements(Id,ProductId,LotId,Quantity,MovementType,OccurredAt,UserId,Reason,CreatedAt) VALUES($id,$p,$l,$q,'RECEPTION',$now,$u,'Réception',$now)", ("$id", Guid.NewGuid().ToString()), ("$p", productId.ToString()), ("$l", lotId), ("$q", quantity), ("$now", now), ("$u", user.Id.ToString())); await tx.CommitAsync();
    }

    public async Task<SaleResult> SellAsync(IReadOnlyList<SaleCartLine> lines, decimal received, string method, User user)
    {
        if (lines.Count == 0) throw new DomainException("Le panier est vide."); var total = lines.Sum(x => x.Quantity * x.UnitPrice); if (received < total && method == "Espèces") throw new DomainException("Le montant reçu est insuffisant.");
        await using var c = factory.Create(); await using var tx = c.BeginTransaction(); var now = DateTimeOffset.UtcNow.ToString("O"); var saleId = Guid.NewGuid().ToString(); var number = $"V-{DateTime.Now:yyyyMMdd-HHmmss}";
        await Exec(c, tx, "INSERT INTO Sales(Id,SaleNumber,SoldAt,UserId,SubtotalMinor,TotalMinor,CreatedAt) VALUES($id,$n,$now,$u,$t,$t,$now)", ("$id", saleId), ("$n", number), ("$now", now), ("$u", user.Id.ToString()), ("$t", Money.ToMinorUnits(total)));
        foreach (var line in lines)
        {
            await using var q = c.CreateCommand(); q.Transaction = tx; q.CommandText = "SELECT Id,Quantity,PurchaseCostMinor FROM StockLots WHERE ProductId=$p AND Quantity >= $q AND IsBlocked=0 AND (ExpiryDate IS NULL OR date(ExpiryDate)>=date('now')) ORDER BY date(ExpiryDate) LIMIT 1"; q.Parameters.AddWithValue("$p", line.ProductId.ToString()); q.Parameters.AddWithValue("$q", line.Quantity); string lotId; long cost; await using (var rr = await q.ExecuteReaderAsync()) { if (!await rr.ReadAsync()) throw new DomainException($"Stock vendable insuffisant pour {line.Designation}."); lotId = rr.GetString(0); cost = rr.GetInt64(2); }
            await Exec(c, tx, "INSERT INTO SaleLines(Id,SaleId,ProductId,LotId,Quantity,UnitPriceMinor,UnitCostMinor) VALUES($id,$s,$p,$l,$q,$price,$cost)", ("$id", Guid.NewGuid().ToString()), ("$s", saleId), ("$p", line.ProductId.ToString()), ("$l", lotId), ("$q", line.Quantity), ("$price", Money.ToMinorUnits(line.UnitPrice)), ("$cost", cost));
            await Exec(c, tx, "UPDATE StockLots SET Quantity=Quantity-$q,UpdatedAt=$now WHERE Id=$l", ("$q", line.Quantity), ("$now", now), ("$l", lotId));
            await Exec(c, tx, "INSERT INTO StockMovements(Id,ProductId,LotId,Quantity,MovementType,OccurredAt,UserId,Reason,CreatedAt) VALUES($id,$p,$l,$q,'VENTE',$now,$u,'Vente',$now)", ("$id", Guid.NewGuid().ToString()), ("$p", line.ProductId.ToString()), ("$l", lotId), ("$q", -line.Quantity), ("$now", now), ("$u", user.Id.ToString()));
        }
        await Exec(c, tx, "INSERT INTO SalePayments(Id,SaleId,Method,AmountMinor) VALUES($id,$s,$m,$a)", ("$id", Guid.NewGuid().ToString()), ("$s", saleId), ("$m", method), ("$a", Money.ToMinorUnits(total))); await tx.CommitAsync(); return new SaleResult(number, total, method == "Espèces" ? received - total : 0);
    }

    public async Task BackupAsync(string destination) { await using var c = factory.Create(); await using var cmd = c.CreateCommand(); cmd.CommandText = "VACUUM INTO $path"; cmd.Parameters.AddWithValue("$path", destination); await cmd.ExecuteNonQueryAsync(); }
    static async Task<int> Exec(SqliteConnection c, SqliteTransaction tx, string sql, params (string, object)[] values) { await using var cmd = c.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql; foreach (var (n, v) in values) cmd.Parameters.AddWithValue(n, v); return await cmd.ExecuteNonQueryAsync(); }
    static async Task<object?> Scalar(SqliteConnection c, SqliteTransaction tx, string sql, params (string, object)[] values) { await using var cmd = c.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql; foreach (var (n, v) in values) cmd.Parameters.AddWithValue(n, v); return await cmd.ExecuteScalarAsync(); }
}

internal static class PasswordHash
{
    public static string Create(string password) { var salt = RandomNumberGenerator.GetBytes(16); var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000, HashAlgorithmName.SHA256, 32); return $"120000:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}"; }
    public static bool Verify(string password, string encoded) { try { var p = encoded.Split(':'); var hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(p[1]), int.Parse(p[0]), HashAlgorithmName.SHA256, 32); return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(p[2])); } catch { return false; } }
}
