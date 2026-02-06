using System.Collections.Concurrent;
using HybridCacheExample.Api.Models;
using HybridCacheExample.Api.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace HybridCacheExample.Api.Services;

public sealed class ProductService(
    HybridCache cache,
    IProductRepository repository,
    ILogger<ProductService> logger)
{
    private readonly HybridCache _cache = cache;
    private readonly IProductRepository _repository = repository;
    private readonly ILogger<ProductService> _logger = logger;
    private int _factoryCalls;

    private static readonly HybridCacheEntryOptions ProductEntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };

    public Task<IReadOnlyCollection<Product>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var key = BuildProductKey(id);
        return _cache.GetOrCreateAsync(
            key,
            async cancel =>
            {
                var factoryInvocation = Interlocked.Increment(ref _factoryCalls);
                _logger.LogInformation("HybridCache miss for key '{Key}'. Factory invocation #{FactoryInvocation}.", key, factoryInvocation);
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancel);
                return await _repository.GetByIdAsync(id, cancel);
            },
            options: ProductEntryOptions,
            tags: ["catalog", $"product:{id}"],
            cancellationToken: cancellationToken);
    }

    public async Task<Product> UpdatePriceAsync(int id, decimal price, CancellationToken cancellationToken = default)
    {
        var updated = await _repository.UpdatePriceAsync(id, price, cancellationToken);
        await _cache.RemoveAsync(BuildProductKey(id), cancellationToken);
        return updated;
    }

    public Task InvalidateProductAsync(int id, CancellationToken cancellationToken = default)
        => _cache.RemoveAsync(BuildProductKey(id), cancellationToken);

    public Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default)
        => _cache.RemoveByTagAsync(tag, cancellationToken);

    public object GetStats() => new
    {
        FactoryCalls = Volatile.Read(ref _factoryCalls)
    };

    public async Task<object> RunStampedeDemoAsync(int id, int concurrency, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(BuildProductKey(id), cancellationToken);

        var before = Volatile.Read(ref _factoryCalls);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var calls = Enumerable.Range(0, concurrency).Select(async _ =>
        {
            await gate.Task.WaitAsync(cancellationToken);
            return await GetByIdAsync(id, cancellationToken);
        }).ToArray();

        gate.TrySetResult(true);
        var products = await Task.WhenAll(calls);
        var after = Volatile.Read(ref _factoryCalls);

        var versions = products
            .Where(x => x is not null)
            .Select(x => x!.Version)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        return new
        {
            ProductId = id,
            Concurrency = concurrency,
            FactoryCallsDuringTest = after - before,
            DistinctVersionsReturned = versions,
            Message = "For a single missing key, FactoryCallsDuringTest should typically be 1."
        };
    }

    private static string BuildProductKey(int id) => $"product:{id}";
}
