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
| Automatic invalidation on `SaveChanges` | `AddCleverCache` + EF Core interceptor |
| Automatic cascade discovery from navigation properties | `AddCleverCache` + interceptor + `UseCleverCache` |

> **Without the interceptor** you only get dependency tree management — you can define cascade rules with `[DependentCaches]` or `AddDependentCache`, and call `RemoveByType<T>()` yourself to invalidate. Cache entries are never evicted automatically; you are responsible for triggering invalidation. The interceptor is what makes CleverCache hands-off.

## 1. Register services

```csharp
builder.Services.AddCleverCache();
```

If you are using `[DependentCaches]` attributes, pass the assemblies to scan during registration. CleverCache will discover and wire up the cascade rules at startup — no EF Core required:

```csharp
builder.Services.AddCleverCache(o => o.ScanAssemblyContaining<Order>());
```

See [Cache Providers](Cache-Providers) for memory, distributed, Redis, and custom provider options.

## 2. Register the EF Core interceptor (optional)

The interceptor is what makes CleverCache automatic — cache entries for changed entity types are evicted the moment `SaveChanges` or `SaveChangesAsync` completes. Without it, you can still use CleverCache by calling `RemoveByType<T>()` yourself.

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

## 3. Register the EF Core middleware (optional)

Only needed if you want CleverCache to scan `DbSet<T>` navigation properties for automatic cascade discovery:

```csharp
app.UseCleverCache<AppDbContext>();
```

This scans the `DbSet<T>` types registered on `AppDbContext` using EF Core metadata to build the invalidation dependency tree. See [Dependent Caches](Dependent-Caches).

> **Prefer `[DependentCaches]` attributes for most projects.** Global DbSet scanning is convenient but wires up every entity in your context — in large schemas this can cause excessive invalidation and high memory usage from tracking too many key associations. `ScanAssemblyContaining` (step 1) gives you the same automatic wiring with opt-in, per-entity control.

> **Not using EF Core navigation scanning?** If you rely only on `[DependentCaches]` attributes, register those via `ScanAssemblyContaining` in step 1 and skip `UseCleverCache` entirely.
