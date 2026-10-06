# CleverCache

Automatic cache invalidation for .NET — tracks entity changes via EF Core and clears related cache entries automatically.

Supports **memory cache** (default), **distributed cache** (`IDistributedCache`), or a **custom provider**.

For full documentation, examples, and configuration options see the [CleverCache wiki](https://github.com/chunty/CleverCache/wiki).

See also:
- [Cache Key Providers](https://github.com/chunty/CleverCache/wiki/Cache-Key-Providers)

## Quick start

```csharp
// Memory cache (default)
builder.Services.AddCleverCache();

// Distributed cache (e.g. Redis)
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = "localhost");
builder.Services.AddCleverCache(o => o.UseDistributedCache());

// Custom provider
builder.Services.AddCleverCache(o => o.UseCustomStore<MyStore>());
```

By default, cache entries use a 4 hour sliding expiration. Change the global default with `CleverCacheOptions.DefaultEntryOptions`, or override an individual MediatR query with `[AutoCache(...)]` expiration properties.

Invalidation is safe against concurrent cache fills and delayed eviction callbacks. An invalidated in-flight factory may return its snapshot to its caller, but cannot publish that snapshot into the cache. Cache hits do not acquire invalidation locks; publication and removal coordinate per key without waiting for query factories.

`EnableAsyncRaceConditionGuard` optionally prevents duplicate same-key factory execution (cache stampedes). It is not required for invalidation correctness.

These guarantees are local to one CleverCache instance. Distributed storage does not distribute the type/key index or invalidation notifications. EF integration invalidates after `SaveChanges`, not after an enclosing transaction commits; outer-transaction and cross-instance coordination remain separate application concerns.
