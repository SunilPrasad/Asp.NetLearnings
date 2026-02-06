namespace HybridCacheExample.Api.Models;

public sealed record Product(
    int Id,
    string Name,
    decimal Price,
    int Version,
    DateTimeOffset UpdatedAtUtc);
