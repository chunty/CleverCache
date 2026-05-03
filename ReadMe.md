CleverCache
====================================================
[![NuGet](https://img.shields.io/nuget/dt/clevercache.svg)](https://www.nuget.org/packages/clevercache) 
[![NuGet](https://img.shields.io/nuget/vpre/clevercache.svg)](https://www.nuget.org/packages/clevercache)

> [!WARNING]
> **V2 contains breaking changes.** EF Core support has moved to a separate package, the startup API has changed, and `[DependentCaches]` attributes must now be registered explicitly. See the **[V1 → V2 Migration Guide](https://github.com/chunty/CleverCache/wiki/Migrating-to-V2)** before upgrading.

**CleverCache** solves the problem of remembering when to invalidate cache entries when underlying data changes — especially when a cache entry contains data from multiple entity types.

With a small amount of configuration, CleverCache automatically tracks entity changes via EF Core and clears related cache entries whenever data is created, updated, or deleted.

## 🚀 MediatR users

Install [`CleverCache.MediatR`](https://www.nuget.org/packages/clevercache.mediatr) for **automatic query caching with zero changes to your handlers** — just add `[AutoCache]` to your query and CleverCache handles the rest, including automatic invalidation.

→ [MediatR Integration wiki](https://github.com/chunty/CleverCache/wiki/MediatR-Integration)

> **Automatic invalidation requires Entity Framework Core.** CleverCache hooks into EF Core as a `SaveChangesInterceptor` — entries are cleared automatically when `SaveChanges` or `SaveChangesAsync` completes. Writes that bypass the change tracker (raw SQL, stored procedures, external services) won't trigger automatic invalidation — see [Bulk Operations](https://github.com/chunty/CleverCache/wiki/Bulk-Operations).
>
> **No EF Core?** You can still use CleverCache for manual invalidation — `RemoveByType<T>()` understands your full dependency tree and cascades automatically.

## Install

```
Install-Package CleverCache
```

Or via the .NET CLI:

```
dotnet add package CleverCache
```

## Quick start

```csharp
// 1. Register services
builder.Services.AddCleverCache();

// 2. Add the interceptor to your DbContext
public class AppDbContext(IInterceptor cleverCacheInterceptor) : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.AddInterceptors(cleverCacheInterceptor);
}

// 3. Register the middleware
app.UseCleverCache<AppDbContext>();

// 4. Use it
public class OrderService(ICleverCache cache, AppDbContext db)
{
    public async Task<List<Order>> GetAllAsync()
        => await cache.GetOrCreateAsync<Order, List<Order>>(
               "orders-all",
               async () => await db.Orders.ToListAsync()
           ) ?? [];
}
```

When any `Order` is saved via EF Core, the `"orders-all"` entry is automatically evicted.

## 📖 Full documentation

| Topic | |
|---|---|
| [Getting Started](https://github.com/chunty/CleverCache/wiki/Getting-Started) | Setup, interceptor registration, EF Core requirement |
| [Caching Data](https://github.com/chunty/CleverCache/wiki/Caching-Data) | `GetOrCreate`, multi-type, entry options |
| [Cache Providers](https://github.com/chunty/CleverCache/wiki/Cache-Providers) | Memory, distributed, Redis, custom |
| [Dependent Caches](https://github.com/chunty/CleverCache/wiki/Dependent-Caches) | `AddKeyToType`, `AddDependentCache`, `[DependentCaches]` attribute |
| [MediatR Integration](https://github.com/chunty/CleverCache/wiki/MediatR-Integration) | `[AutoCache]`, `[InvalidatesCache]`, pipeline setup |
| [Bulk Operations](https://github.com/chunty/CleverCache/wiki/Bulk-Operations) | `ExecuteDelete`/`ExecuteUpdate` workarounds |
| [Unit Testing](https://github.com/chunty/CleverCache/wiki/Unit-Testing) | `FakeCache`, mocking `ICleverCache` |
