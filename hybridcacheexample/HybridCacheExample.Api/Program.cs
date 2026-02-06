using HybridCacheExample.Api.Repositories;
using HybridCacheExample.Api.Services;
using Microsoft.Extensions.Caching.Hybrid;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IProductRepository, InMemoryProductRepository>();
builder.Services.AddSingleton<ProductService>();

var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
    });
}

builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 1024 * 1024;
    options.MaximumKeyLength = 1024;
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Ok(new
{
    message = "HybridCache sample is running.",
    swagger = "/swagger"
}));

var products = app.MapGroup("/api/products");

products.MapGet("/", async (ProductService service, CancellationToken token) =>
{
    var all = await service.GetAllAsync(token);
    return Results.Ok(all);
});

products.MapGet("/{id:int}", async (int id, ProductService service, CancellationToken token) =>
{
    var product = await service.GetByIdAsync(id, token);
    return product is null ? Results.NotFound() : Results.Ok(product);
});

products.MapPut("/{id:int}/price/{price:decimal}", async (int id, decimal price, ProductService service, CancellationToken token) =>
{
    var updated = await service.UpdatePriceAsync(id, price, token);
    return Results.Ok(updated);
});

products.MapPost("/invalidate/{id:int}", async (int id, ProductService service, CancellationToken token) =>
{
    await service.InvalidateProductAsync(id, token);
    return Results.Ok(new { message = $"Invalidated product:{id}" });
});

var cache = app.MapGroup("/api/cache");

cache.MapPost("/tags/{tag}/invalidate", async (string tag, ProductService service, CancellationToken token) =>
{
    await service.InvalidateTagAsync(tag, token);
    return Results.Ok(new { message = $"Invalidated tag '{tag}'" });
});

cache.MapGet("/stats", (ProductService service) => Results.Ok(service.GetStats()));

var demo = app.MapGroup("/api/demo");

demo.MapPost("/stampede/{id:int}", async (int id, int concurrency, ProductService service, CancellationToken token) =>
{
    var safeConcurrency = Math.Clamp(concurrency, 2, 200);
    var result = await service.RunStampedeDemoAsync(id, safeConcurrency, token);
    return Results.Ok(result);
});

app.Run();
