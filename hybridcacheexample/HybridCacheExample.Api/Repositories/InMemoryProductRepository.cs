using System.Collections.Concurrent;
using HybridCacheExample.Api.Models;

namespace HybridCacheExample.Api.Repositories;

public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<int, Product> _products = new(
        [
            new KeyValuePair<int, Product>(1, new Product(1, "Mechanical Keyboard", 129.99m, 1, DateTimeOffset.UtcNow)),
            new KeyValuePair<int, Product>(2, new Product(2, "4K Monitor", 399.00m, 1, DateTimeOffset.UtcNow)),
            new KeyValuePair<int, Product>(3, new Product(3, "Wireless Mouse", 59.50m, 1, DateTimeOffset.UtcNow))
        ]);

    public Task<IReadOnlyCollection<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Product> all = _products.Values.OrderBy(x => x.Id).ToArray();
        return Task.FromResult(all);
    }

    public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        _products.TryGetValue(id, out var product);
        return Task.FromResult(product);
    }

    public Task<Product> UpdatePriceAsync(int id, decimal price, CancellationToken cancellationToken = default)
    {
        var updated = _products.AddOrUpdate(
            id,
            key => new Product(key, $"Generated Product {key}", price, 1, DateTimeOffset.UtcNow),
            (_, existing) => existing with
            {
                Price = price,
                Version = existing.Version + 1,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });

        return Task.FromResult(updated);
    }
}
