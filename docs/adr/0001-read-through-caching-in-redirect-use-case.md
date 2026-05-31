# Read-through caching in RedirectUseCase

We cache `ShortUrl` entities in an in-process `IMemoryCache` inside `RedirectUseCase` (not in the repository layer) to eliminate redundant database reads on the redirect hot path. The cache uses a fixed 5-minute TTL with a 10,000-entry size limit, namespaced keys (`"slug:{slug}"`), and `GetOrCreateAsync` for stampede protection. Expiration correctness is handled by the existing `ExpiresAt` check on every cache hit, not by per-entry TTL alignment.

## Considered Options

- **Cache in `UrlRepository`** — rejected because the create path must always hit the database to guarantee slug uniqueness. Placing the cache in the repository would either require carve-outs for writes or risk serving stale data during slug-collision checks.
- **`IDistributedCache` (Redis)** — rejected for now. The system already assumes single-process deployment (singleton `InMemoryClickCounter`). Distributed caching adds network latency and operational complexity that isn't justified until we scale to multiple instances, at which point both the click counter and the cache move to Redis together.
- **Negative caching** — rejected because the 10,000-entry limit is better spent on real entries, the unique index makes miss queries fast, and DDoS protection belongs at the infrastructure layer (rate limiting / reverse proxy).

## Consequences

- Redirect p95/p99 latency improves significantly for hot slugs (eliminates DB round-trip on cache hit).
- Cache settings (`CacheTtlMinutes`, `CacheSizeLimit`) are exposed via `appsettings.json` using `IOptions<CacheSettings>` for deploy-time tuning without recompile.
- If a delete/update API is added in the future, explicit cache invalidation will be needed in `RedirectUseCase`.
