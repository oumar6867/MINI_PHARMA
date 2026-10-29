namespace Pharmacie.Domain;

public sealed record Product(Guid Id, string Reference, string Designation, Guid CategoryId, string? Dosage, string? Forme, string UniteVente, string? CodeBarres, decimal PrixVente, decimal SeuilMinimum, bool Actif = true);
public sealed record Category(Guid Id, string Nom);
public sealed record StockLot(Guid Id, Guid ProductId, string NumeroLot, DateOnly? DateExpiration, decimal Quantite, decimal CoutUnitaire);
public sealed record User(Guid Id, string NomUtilisateur, string NomComplet, string Role);
public static class Money { public static long ToMinorUnits(decimal value) => decimal.ToInt64(decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero)); public static decimal FromMinorUnits(long value) => value / 100m; }
public sealed class DomainException(string message) : Exception(message);
