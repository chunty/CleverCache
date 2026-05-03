# CleverCache

**CleverCache** solves the problem of remembering when to invalidate cache entries when the underlying data changes — especially when a single cache entry contains data from multiple entity types.

With a small amount of configuration, CleverCache automatically tracks changes via EF Core and clears related cache entries when any tracked entity is created, updated, or deleted.

## Documentation

| Topic | Description |
|---|---|
| [Getting Started](Getting-Started) | Install, configure EF Core interceptor, register services |
| [Caching Data](Caching-Data) | `GetOrCreate`, multi-type associations, entry options |
| [Cache Providers](Cache-Providers) | Memory, distributed, Redis, custom providers |
| [Dependent Caches](Dependent-Caches) | `AddKeyToType`, `AddDependentCache`, `[DependentCaches]` attribute |
| [MediatR Integration](MediatR-Integration) | `[AutoCache]`, `[InvalidatesCache]`, pipeline setup |
| [Bulk Operations](Bulk-Operations) | Handling `ExecuteDelete`/`ExecuteUpdate` and non-EF writes |
| [Unit Testing](Unit-Testing) | `FakeCache`, mocking `ICleverCache` |

## Packages

| Package | NuGet | Purpose |
|---|---|---|
| `CleverCache` | [![NuGet](https://img.shields.io/nuget/vpre/clevercache.svg)](https://www.nuget.org/packages/clevercache) | Core package |
| `CleverCache.MediatR` | [![NuGet](https://img.shields.io/nuget/vpre/clevercache.mediatr.svg)](https://www.nuget.org/packages/clevercache.mediatr) | MediatR pipeline behaviours |
| `CleverCache.Redis` | [![NuGet](https://img.shields.io/nuget/vpre/clevercache.redis.svg)](https://www.nuget.org/packages/clevercache.redis) | Redis cache provider |

> **Requires EF Core.** Automatic invalidation hooks into `SaveChanges`/`SaveChangesAsync` via a `SaveChangesInterceptor`. Without EF Core you can still use CleverCache for manual invalidation — see [Bulk Operations](Bulk-Operations).
