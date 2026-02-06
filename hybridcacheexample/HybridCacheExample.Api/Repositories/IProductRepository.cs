using HybridCacheExample.Api.Models;

namespace HybridCacheExample.Api.Repositories;

public interface IProductRepository
{
    Task<IReadOnlyCollection<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Product> UpdatePriceAsync(int id, decimal price, CancellationToken cancellationToken = default);
}
