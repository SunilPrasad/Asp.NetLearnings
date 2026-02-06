# HybridCache ASP.NET Sample

This sample shows how to use `HybridCache` in a minimal ASP.NET Core API.

## Prerequisites

- .NET SDK 9.0+
- Optional: Redis (for secondary distributed cache)

## Run

```powershell
cd hybridcacheexample/HybridCacheExample.Api
dotnet restore
dotnet run
```

Swagger:
OpenAPI document:
- `http://localhost:5219/openapi/v1.json`

## Optional Redis

Set a Redis connection string in `appsettings.json`:

```json
"ConnectionStrings": {
  "Redis": "localhost:6379"
}
```

If `Redis` is empty, HybridCache still works with local in-process caching.

## Endpoints

- `GET /api/products`
- `GET /api/products/{id}`
- `PUT /api/products/{id}/price/{price}`
- `POST /api/products/invalidate/{id}`
- `POST /api/cache/tags/{tag}/invalidate`
- `GET /api/cache/stats`
- `POST /api/demo/stampede/{id}?concurrency=50`

## Stampede Demo

Call:

```http
POST /api/demo/stampede/1?concurrency=50
```

Expected behavior:
- `FactoryCallsDuringTest` is typically `1` for a single missing key.
- This shows that concurrent callers for the same key are coalesced in `GetOrCreateAsync`.
