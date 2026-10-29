using Pharmacie.Domain;
namespace Pharmacie.Tests;
public class DomainTests
{
 [Fact] public void Money_Uses_Cents_With_Explicit_Rounding() { Assert.Equal(1235, Money.ToMinorUnits(12.345m)); Assert.Equal(12.35m, Money.FromMinorUnits(1235)); }
 [Fact] public void Product_Reference_Is_Required() { Assert.ThrowsAsync<DomainException>(()=>new Pharmacie.Application.ProductService(new FakeRepository()).CreateAsync(new Product(Guid.NewGuid(),"","Test",Guid.NewGuid(),null,null,"boîte",null,1,0))); }
 private sealed class FakeRepository : IProductRepository { public Task AddAsync(Product product,CancellationToken cancellationToken=default)=>Task.CompletedTask; public Task<Product?> GetByIdAsync(Guid id,CancellationToken cancellationToken=default)=>Task.FromResult<Product?>(null); public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<Product>>(Array.Empty<Product>()); }
}
