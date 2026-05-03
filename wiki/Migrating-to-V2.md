# Migrating from V1 to V2

V2 is a significant refactor. The core caching API is mostly the same but several breaking changes tighten the design and reduce coupling. This page documents every change with before/after examples.

---

## Summary of breaking changes

| Area | V1 | V2 |
|---|---|---|
| EF Core dependency | Bundled in `CleverCache` | Separate `CleverCache.EntityFrameworkCore` package |
| Startup (interceptor) | `AddCleverCache()` registered interceptor | `AddCleverCacheEntityFramework()` required |
| Startup (scanning) | `app.UseCleverCache<TContext>()` | `app.ScanDbSetsForCacheDependencies<TContext>()` |
| Scan options | `CleverCacheOptions.Scanning` | Passed directly to `ScanDbSetsForCacheDependencies` |
| `GetOrCreate` factory | `Func<ICacheEntry, TItem>` | `Func<TItem>` |
| `GetOrCreateAsync` factory | `Func<ICacheEntry, Task<TItem>>` | `Func<Task<TItem>>` |
| Cache entry options type | `MemoryCacheEntryOptions` | `CleverCacheEntryOptions` |
| Dependent cache attribute | `[DependantCaches]` | `[DependentCaches]` |
| `FakeCache` namespace | `CleverCache.Implementations` | `CleverCache` |

---

## Step-by-step migration

### 1. Install the new EF Core package

V2 moves all EF Core concerns into a dedicated package. Your main project no longer depends on `Microsoft.EntityFrameworkCore`.

**Before** — one package does everything:
```
dotnet add package CleverCache
```

**After** — install both:
```
dotnet add package CleverCache
dotnet add package CleverCache.EntityFrameworkCore
```

---

### 2. Update DI registration

`AddCleverCache()` no longer registers the EF Core interceptor. Replace both calls with a single `AddCleverCacheEntityFramework()`, which registers the interceptor and calls `AddCleverCache()` internally.

**Before:**
```csharp
builder.Services.AddCleverCache();
```

**After:**
```csharp
builder.Services.AddCleverCacheEntityFramework();
// with CleverCache options (e.g. assembly scanning):
builder.Services.AddCleverCacheEntityFramework(o => o.ScanAssemblyContaining<Order>());
```

> If you are not using EF Core (manual invalidation only), keep using `AddCleverCache()` — `AddCleverCacheEntityFramework` is only needed if you have the EF package installed.

---

### 3. Update `UseCleverCache` → `ScanDbSetsForCacheDependencies`

`app.UseCleverCache<TContext>()` has been replaced with a more descriptive name that lives on `IApplicationBuilder`.

**Before:**
```csharp
app.UseCleverCache<AppDbContext>();
```

**After:**
```csharp
app.ScanDbSetsForCacheDependencies<AppDbContext>();
```

Scan options are now passed directly instead of being stored on `CleverCacheOptions`.

**Before:**
```csharp
builder.Services.AddCleverCache(o =>
{
    o.Scanning.NavigationScanMode = DependentCacheNavigationScanMode.Direct;
    o.Scanning.ReverseNavigationDependencies = true;
});
app.UseCleverCache<AppDbContext>();
```

**After:**
```csharp
builder.Services.AddCleverCacheEntityFramework();
// ...
app.ScanDbSetsForCacheDependencies<AppDbContext>(o =>
{
    o.NavigationScanMode = DependentCacheNavigationScanMode.Direct;
    o.ReverseNavigationDependencies = true;
});
```

Multiple DbContexts each get their own call with independent options:

```csharp
app.ScanDbSetsForCacheDependencies<AppDbContext>();
app.ScanDbSetsForCacheDependencies<ReportingDbContext>(o =>
    o.NavigationScanMode = DependentCacheNavigationScanMode.Recursive);
```

---

### 4. Update `GetOrCreate` / `GetOrCreateAsync` factory signatures

The factory delegate no longer receives an `ICacheEntry`. Cache entry options are now a separate parameter of type `CleverCacheEntryOptions` instead of `MemoryCacheEntryOptions`.

**Before:**
```csharp
var result = await cache.GetOrCreateAsync<List<Order>>(
    [typeof(Order)],
    "orders-all",
    async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
        return await db.Orders.ToListAsync();
    });
```

**After:**
```csharp
var result = await cache.GetOrCreateAsync<List<Order>>(
    [typeof(Order)],
    "orders-all",
    async () => await db.Orders.ToListAsync(),
    new CleverCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
```

The synchronous overload changes identically:

**Before:**
```csharp
var result = cache.GetOrCreate<int>(
    [typeof(Order)],
    "order-count",
    entry => db.Orders.Count());
```

**After:**
```csharp
var result = cache.GetOrCreate<int>(
    [typeof(Order)],
    "order-count",
    () => db.Orders.Count());
```

---

### 5. Update the `[DependantCaches]` attribute

The attribute class was renamed to fix a spelling mistake. The property `DependantTypes` retains the original spelling for now.

**Before:**
```csharp
[DependantCaches([typeof(OrderLine), typeof(OrderNote)])]
public class Order { }
```

**After:**
```csharp
[DependentCaches([typeof(OrderLine), typeof(OrderNote)])]
public class Order { }
```

`[DependentCaches]` now also accepts a `NavigationScanMode` parameter to control whether navigation properties on the decorated type are additionally scanned:

```csharp
[DependentCaches([typeof(OrderLine)], navigationScanMode: DependentCacheNavigationScanMode.Direct)]
public class Order { }
```

---

### 6. Register `[DependentCaches]` attributes at startup

In V1, `UseCleverCache` processed `[DependantCaches]` attributes automatically via EF model scanning. In V2 you have two options:

**Option A — attribute-only (no EF model scan):** use `ScanAssemblyContaining` in `AddCleverCache`. Works without the EF package and is the recommended approach when you don't need navigation scanning.

```csharp
builder.Services.AddCleverCache(o => o.ScanAssemblyContaining<Order>());
```

**Option B — EF navigation scan (also picks up attributes):** call `ScanDbSetsForCacheDependencies`. This scans navigations AND processes `[DependentCaches]` attributes on each entity type in the model.

```csharp
app.ScanDbSetsForCacheDependencies<AppDbContext>(o =>
    o.NavigationScanMode = DependentCacheNavigationScanMode.Direct);
```

---

### 7. Update `FakeCache` namespace

`FakeCache` has moved from the `CleverCache.Implementations` namespace to the root `CleverCache` namespace.

**Before:**
```csharp
using CleverCache.Implementations;

mocker.Use<ICleverCache>(new FakeCache());
```

**After:**
```csharp
// No extra using needed — FakeCache is in the CleverCache namespace
mocker.Use<ICleverCache>(new FakeCache());
```

---

### 8. DbContext constructor injection (optional cleanup)

V1 required you to inject `IInterceptor` or `CleverCacheInterceptor` into your DbContext constructor. This still works in V2 (the interceptor is still registered as `IInterceptor`), but the preferred V2 pattern is to wire it up in `AddDbContext` using the extension method:

**V1 (still valid in V2 — no change required):**
```csharp
public class AppDbContext(IInterceptor cleverCacheInterceptor) : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.AddInterceptors(cleverCacheInterceptor);
}
```

**V2 preferred (no DbContext constructor changes):**
```csharp
// Program.cs
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString);
    options.AddCleverCache(sp); // extension from CleverCache.EntityFrameworkCore
});

// AppDbContext.cs — no interceptor injection needed
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options) { }
```

---

## New features in V2

These aren't breaking changes but are worth knowing about:

- **`ScanAssemblyContaining<T>()`** — scan assemblies for `[DependentCaches]` attributes without EF Core. Replaces manually calling `AddDependentCache` for each type.

- **`[InvalidatesCache]` for MediatR** — decorate any command with `[InvalidatesCache(typeof(Order))]` to evict cache entries automatically after the command succeeds.

- **`InvalidateCaches` bulk extension** — chain onto `ExecuteDelete` / `ExecuteDeleteAsync` results for bulk operations that bypass the change tracker.

- **Distributed cache and Redis** — `CleverCache.Redis` is now the only way to bring in StackExchange.Redis. The `AddCleverCache()` options no longer accept `UseRedisCache`.

- **Custom cache provider** — implement `ICleverCacheStore` and register with `AddCleverCache(o => o.UseCustomStore<MyStore>())`.

- **Stampede protection** — concurrent requests for the same key now only invoke the factory once.
