# Design Doc: Read-Through Cache for Redirect Lookups

## Problem

Every redirect request (`GET /{slug}`) hits the database to resolve the Slug to its Original URL. At 1000 RPS sustained load, this creates unnecessary database pressure for popular slugs that are accessed repeatedly. Stress tests show p99 latency of 148ms at 1000 RPS, primarily due to SQLite's single-writer WAL contention under concurrent read/write load.

## Solution

Add an in-process read-through cache in `RedirectUseCase` that stores recently-accessed `ShortUrl` entities in memory, eliminating database round-trips for hot slugs.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| What to cache | Full `ShortUrl` entity | Need both `OriginalUrl` and `ExpiresAt` to evaluate expiration without DB access |
| Invalidation | Fixed TTL + runtime `ExpiresAt` check | TTL controls memory; existing expiration logic handles correctness |
| Technology | `IMemoryCache` | Matches single-process deployment model (consistent with singleton `InMemoryClickCounter`) |
| Placement | `RedirectUseCase` only | Create path must always hit DB for slug uniqueness guarantees |
| TTL | 5 minutes | Keeps hot slugs warm; staleness is not a concern since URLs are immutable |
| Size limit | 10,000 entries (~2 MB) | Covers the hot set at current throughput with bounded memory |
| Negative caching | No | Preserve cache slots for real entries; index makes miss queries fast |
| Configuration | `appsettings.json` via `IOptions<CacheSettings>` | Deploy-time tuning without recompile |
| Injection | `IMemoryCache` directly | Standard framework interface; no custom abstraction needed |
| Cache key | `"slug:{slug}"` | Namespaced to prevent collisions with future cache consumers |
| Concurrency | `GetOrCreateAsync` | Built-in stampede protection; serializes factory for same key |

## Architecture

```
┌─────────────────────────────────────────────────┐
│                  GET /{slug}                     │
└─────────────────────┬───────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│              RedirectUseCase                     │
│                                                 │
│  1. cache.GetOrCreateAsync("slug:{slug}", ...)  │
│     ├─ HIT  → return cached ShortUrl            │
│     └─ MISS → call repository.GetBySlugAsync()  │
│              → store in cache (TTL=5m, Size=1)  │
│              → return ShortUrl                  │
│                                                 │
│  2. Check ExpiresAt → return Gone if expired    │
│  3. clickCounter.Increment(slug)                │
│  4. Return OriginalUrl                          │
└─────────────────────────────────────────────────┘
```

## Configuration

```json
{
  "CacheSettings": {
    "TtlMinutes": 5,
    "SizeLimit": 10000
  }
}
```

## Implementation Plan

### 1. Add `CacheSettings` class

A simple POCO bound from `appsettings.json`:
- `TtlMinutes` (int, default: 5)
- `SizeLimit` (int, default: 10000)

### 2. Register `IMemoryCache` in DI

Configure `MemoryCache` with `SizeLimit` from settings. Register `CacheSettings` via `IOptions<CacheSettings>`.

### 3. Modify `RedirectUseCase`

- Add `IMemoryCache` and `IOptions<CacheSettings>` to constructor
- Replace direct `_repository.GetBySlugAsync(slug)` with `GetOrCreateAsync` pattern
- Set `AbsoluteExpirationRelativeToNow` = TTL from settings
- Set `Size = 1` on each entry for size-limit accounting
- Cache key: `$"slug:{slug}"`
- Only cache when entity is found (no negative caching)

### 4. Update `Program.cs`

- `builder.Services.AddMemoryCache()`
- Bind `CacheSettings` from configuration
- Pass `IMemoryCache` into `RedirectUseCase` registration

### 5. Update tests

- Use real `MemoryCache` instance in `RedirectUseCase` tests
- Verify: cache hit avoids second repository call
- Verify: expired URLs still return Gone even when cached
- Verify: not-found slugs are not cached

## What This Does NOT Change

- **Create path** (`ShortenUrlUseCase`): Always hits the DB directly — no caching
- **Click counting**: Still uses `InMemoryClickCounter` + `ClickCountFlusher` (unchanged)
- **Stats endpoint**: Not cached (low traffic, needs fresh click count)

## Future Considerations

- If a delete/update API is added, `RedirectUseCase` will need explicit cache eviction on mutation
- If scaling to multiple instances, migrate both `IMemoryCache` and `InMemoryClickCounter` to Redis together
- Size limit can be bumped via config if traffic grows beyond 10K unique slugs per 5-minute window
