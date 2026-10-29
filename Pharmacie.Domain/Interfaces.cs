namespace Pharmacie.Domain;
public interface IProductRepository { Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default); Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default); Task AddAsync(Product product, CancellationToken cancellationToken = default); }
public interface IDatabaseInitializer { Task InitializeAsync(CancellationToken cancellationToken = default); }
