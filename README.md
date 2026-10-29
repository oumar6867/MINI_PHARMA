# Pharmacie

Socle d'une application de gestion de mini-pharmacie hors ligne, en français, basée sur WinForms, .NET 10 LTS, SQLite et ADO.NET.

## Architecture

- `Pharmacie.Domain` : entités, règles et contrats.
- `Pharmacie.Application` : cas d'utilisation et services.
- `Pharmacie.Infrastructure` : SQLite, connexion sécurisée, schéma/migration initiale et dépôts.
- `Pharmacie.WinForms` : interface Windows Forms.
- `Pharmacie.Tests` : tests xUnit.

Les montants sont stockés en centimes (`INTEGER`) dans SQLite et manipulés en `decimal` dans le domaine. Les clés étrangères sont activées sur chaque connexion, les requêtes sont paramétrées et le schéma est idempotent.

## Commandes

```powershell
dotnet restore Pharmacie.slnx
dotnet build Pharmacie.slnx --configuration Release
dotnet test Pharmacie.slnx --configuration Release
dotnet run --project Pharmacie.WinForms
```

La base est créée automatiquement dans `%LOCALAPPDATA%\Pharmacie\pharmacie.db`. Au premier lancement, connectez-vous avec `admin` / `admin`, puis remplacez ce mot de passe dans une version de production.

## État du socle

La version actuelle fournit une connexion sécurisée par hachage PBKDF2, un tableau de bord, le catalogue produits, les réceptions et lots, la vente FEFO avec contrôle du stock, les paiements, les alertes de péremption et une sauvegarde SQLite. Les modules avancés de retours, inventaires, clôture de caisse et exports PDF sont prévus dans les prochains incréments.
