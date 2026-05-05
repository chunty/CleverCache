# Getting Started

## Install

```
Install-Package CleverCache
```

Or via the .NET CLI:

```
dotnet add package CleverCache
```

## Usage paths

CleverCache can be used with or without EF Core:

| Scenario | What you need |
|---|---|
| Manual or attribute-driven invalidation | `AddCleverCache` only |
| Automatic invalidation on `SaveChanges` | `AddCleverCacheEntityFramework` |
| Automatic cascade discovery from navigation properties | `AddCleverCacheEntityFramework` + `ScanDbSetsForCacheDependencies` |

> **Without the interceptor** you only get dependency tree management — you can define cascade rules with `[DependentCaches]` or `AddDependentCache`, and call `RemoveByType<T>()` yourself to invalidate. Cache entries are never evicted automatically; you are responsible for triggering invalidation. The interceptor is what makes CleverCache hands-off.

## 1. Register services

**Without EF Core** (manual or attribute-driven invalidation only):

```csharp
builder.Services.AddCleverCache();
// optionally scan assemblies for [DependentCaches] attributes:
builder.Services.AddCleverCache(o => o.ScanAssemblyContaining<Order>());
```

**With EF Core** — `AddCleverCacheEntityFramework` registers the interceptor and calls `AddCleverCache` internally, so a single call is all you need:

```csharp
builder.Services.AddCleverCacheEntityFramework();
// with CleverCache options:
builder.Services.AddCleverCacheEntityFramework(o => o.ScanAssemblyContaining<Order>());
```

See [Cache Providers](Cache-Providers) for memory, distributed, Redis, and custom provider options.

## 2. Register the EF Core interceptor (optional)

The interceptor is what makes CleverCache automatic — cache entries for changed entity types are evicted the moment `SaveChanges` or `SaveChangesAsync` completes. Without it, you can still use CleverCache by calling `RemoveByType<T>()` yourself.

Install the EF Core package:

```
dotnet add package CleverCache.EntityFrameworkCore
```

`AddCleverCacheEntityFramework()` registers both the core services and the interceptor, so you only need one call (see step 1 above).

> **Important:** Automatic invalidation only fires for writes that go through EF Core's change tracker. Writes that bypass it — `ExecuteUpdate`, `ExecuteDelete`, raw SQL, stored procedures, or external services — will **not** trigger invalidation. See [Bulk Operations](Bulk-Operations) for workarounds.

Inject the interceptor into your `DbContext`:

```csharp
// Option A — single interceptor
public class AppDbContext(IInterceptor cleverCacheInterceptor) : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.AddInterceptors(cleverCacheInterceptor);
}

// Option B — alongside existing interceptors
public class AppDbContext(IInterceptor[] interceptors) : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.AddInterceptors(interceptors);
}

// Option C — concrete type
public class AppDbContext(CleverCacheInterceptor cleverCacheInterceptor) : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.AddInterceptors(cleverCacheInterceptor);
}
```

## 3. Scan DbSet navigation properties (optional)

Only needed if you want CleverCache to auto-discover cascade rules from EF Core navigation properties. This can be called multiple times for multiple DbContext types, each with their own scan options.

**ASP.NET Core apps** — call on `IApplicationBuilder`:

```csharp
app.ScanDbSetsForCacheDependencies<AppDbContext>();

// With navigation scanning options:
app.ScanDbSetsForCacheDependencies<AppDbContext>(o =>
    o.NavigationScanMode = DependentCacheNavigationScanMode.Direct);

// Multiple contexts with different options:
app.ScanDbSetsForCacheDependencies<OrderDbContext>(o =>
    o.NavigationScanMode = DependentCacheNavigationScanMode.Direct);
app.ScanDbSetsForCacheDependencies<CatalogDbContext>();
```

**Worker services and console apps** — `IApplicationBuilder` isn't available, call on `IServiceProvider` instead:

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => services.AddCleverCacheEntityFramework())
    .Build();

host.Services.ScanDbSetsForCacheDependencies<AppDbContext>();

await host.RunAsync();
```

See [Dependent Caches](Dependent-Caches) for full details on navigation scanning modes.

> **Prefer `[DependentCaches]` attributes for most projects.** Global DbSet scanning wires up every entity in your context — in large schemas this can cause excessive invalidation and high memory usage from tracking too many key associations. `ScanAssemblyContaining` (step 1) gives you the same automatic wiring with opt-in, per-entity control.

> **Not using EF Core navigation scanning?** Skip this step entirely — `[DependentCaches]` attributes via `ScanAssemblyContaining` don't need it.
