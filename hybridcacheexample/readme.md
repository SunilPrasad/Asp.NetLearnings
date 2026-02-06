# HybridCache in ASP.NET Core 9: Practical Guide


## 2. What is HybridCache?

`HybridCache` is a high-level cache abstraction that combines:

- Local in-process cache (fast access, memory-based)
- Optional distributed cache backend (Redis/SQL/Postgres/etc.)

So you get a two-layer strategy with one API:

1. Check local cache
2. Check distributed cache (if configured)
3. On miss, run data factory once and populate both layers

Core API you use most:
- `GetOrCreateAsync(...)`

## 3. Why teams like it for day-to-day coding

With older patterns, developers usually write repetitive cache-aside code:
- Build key
- Try get bytes
- Deserialize
- Fetch source when miss
- Serialize and set
- Handle race conditions manually

`HybridCache` simplifies all of that into one consistent API and adds safety features like stampede protection.

## 4. HybridCache vs IMemoryCache vs IDistributedCache

| Capability | IMemoryCache | IDistributedCache | HybridCache |
|---|---|---|---|
| Scope | Single process | Shared across instances | Local + shared (if configured) |
| Built-in cache-aside convenience | Medium | Low | High (`GetOrCreateAsync`) |
| Serialization burden | Usually none | Manual in app code | Built-in defaults + configurable |
| Stampede protection | Manual | Manual | Built-in per key concurrency control |
| Tag-based invalidation | No | No | Yes (`RemoveByTagAsync`) |
| Local hot-path speed | High | Lower than memory | High (local first) |
| Cross-instance consistency | No | Better | Better via secondary cache |
| Boilerplate reduction | Low | Low | High |

### Practical takeaway

- Choose `IMemoryCache` when app is single-instance and simple.
- Choose `IDistributedCache` when you need cross-instance cache sharing but can tolerate manual boilerplate.
- Choose `HybridCache` when you want both performance and simpler code with fewer mistakes.

## 5. Real-world use cases

### Use case A: Product catalog API (e-commerce)

Problem:
- Product detail endpoints are read-heavy.
- Data changes occasionally but is requested constantly.

How HybridCache helps:
- Local cache serves repeat calls quickly.
- Distributed layer shares cache entries across app instances.
- Stampede protection reduces DB spikes during expiration windows.

Outcome:
- Lower p95 latency, fewer DB calls, better burst handling.

### Use case B: Dashboard aggregation (admin portal)

Problem:
- Dashboard composes data from multiple services.
- Same aggregation gets requested by many users around the same time.

How HybridCache helps:
- `GetOrCreateAsync` wraps expensive aggregation logic cleanly.
- One concurrent computation per key inside each app instance.
- Optional tags allow bulk invalidation (for example, per tenant).

Outcome:
- Faster dashboard render and more predictable backend load.

### Use case C: User profile + permissions snapshot

Problem:
- Profile and permission checks happen on many requests.
- Data should refresh quickly after admin changes.

How HybridCache helps:
- Cache profile snapshot by user id.
- Invalidate by tag after permission updates.
- Keep short local expiration and slightly longer global expiration where needed.

Outcome:
- Less repeated service/database calls while preserving update responsiveness.

### Use case D: Multi-instance microservice reads from Redis + SQL fallback

Problem:
- Each pod repeatedly fetches the same expensive read model.

How HybridCache helps:
- Local cache lowers network hops to Redis.
- Secondary distributed cache avoids hitting SQL repeatedly across pods.
- Configurable serializers reduce custom coding.

Outcome:
- Reduced infrastructure pressure and cleaner service code.

## 6. Key advantages over IMemoryCache and IDistributedCache

1. One API for common cache-aside flow
- Less repetitive code, fewer implementation bugs.

2. Built-in stampede protection
- Concurrent callers for the same key wait on one factory call.

3. First-class two-level caching pattern
- Fast local reads + shared distributed cache.

4. Tag-based invalidation
- Invalidate groups logically without hand-maintaining key registries.

5. Better serializer story
- Defaults are already usable; custom serializers are supported.

6. Entry policy controls
- Separate local expiration and overall expiration options.

## 7. Why HybridCache protects against cache stampede

A cache stampede happens when many requests hit the same expired or missing key at the same time, and all of them call the backend (DB/API) together.

HybridCache reduces this with per-key request coalescing in `GetOrCreateAsync`:

1. First request for a missing key enters the factory delegate.
2. Concurrent requests for that same key wait instead of running duplicate factories.
3. When the first factory finishes, HybridCache stores the value and releases waiters.
4. All waiting requests get the same computed result, and later requests hit cache.

Why this matters in production:

- It prevents sudden backend spikes during key expiration windows.
- It lowers duplicate expensive work (queries, external API calls, serialization).
- It stabilizes latency under burst traffic for hot keys.

Compared to older approaches:

- `IMemoryCache`: you usually implement your own locking or lazy wrappers.
- `IDistributedCache`: you usually implement manual cache-aside and locking strategy yourself.
- `HybridCache`: this behavior is built into the main flow (`GetOrCreateAsync`) so you write less defensive code.

## 8. Important behaviors and caveats

1. Key design still matters
- Build stable, unique keys.
- Avoid raw untrusted user input as key material.

2. Stampede protection scope
- Stampede protection is per key and prevents duplicate concurrent factory execution in normal app runtime behavior.
- Do not treat it as a cross-cluster global distributed lock for every node/process.

3. Tag invalidation is logical
- Entries are treated as misses after invalidation; underlying storage cleanup still follows expiration behavior.

4. Size limits exist
- `MaximumPayloadBytes` default: 1 MB.
- `MaximumKeyLength` default: 1024 chars.
- Oversized entries/keys are skipped and logged.

5. Distributed caching still needs serialization choices
- Default serializer works for many scenarios; validate performance for large/high-throughput objects.

## 9. Suggested default configuration for production starters

- Global expiration: 5-15 minutes for stable read models.
- Local cache expiration: shorter than distributed (for example, 1-5 minutes) when freshness is important.
- Tag strategy: align with domain groups like `tenant:{id}`, `catalog`, `user:{id}`.
- Guardrails: set payload and key limits explicitly.
- Observe: add cache hit/miss and factory execution metrics.


## 10. References

- ASP.NET Core 9 release notes (HybridCache section):
  https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-9.0?view=aspnetcore-9.0
- HybridCache docs:
  https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-9.0
- Caching overview:
  https://learn.microsoft.com/en-us/aspnet/core/performance/caching/overview?view=aspnetcore-9.0
- GA announcement (.NET Blog, March 12, 2025):
  https://devblogs.microsoft.com/dotnet/hybrid-cache-is-now-ga/
