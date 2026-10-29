using Pharmacie.Domain;
namespace Pharmacie.Application;
public sealed class ProductService(IProductRepository products)
{
    public Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken = default) => products.GetAllAsync(cancellationToken);
    public async Task CreateAsync(Product product, CancellationToken cancellationToken = default)
    { if (string.IsNullOrWhiteSpace(product.Reference)) throw new DomainException("La référence produit est obligatoire."); if (string.IsNullOrWhiteSpace(product.Designation)) throw new DomainException("La désignation produit est obligatoire."); if (product.PrixVente < 0 || product.SeuilMinimum < 0) throw new DomainException("Les montants et seuils ne peuvent pas être négatifs."); await products.AddAsync(product, cancellationToken); }
}
