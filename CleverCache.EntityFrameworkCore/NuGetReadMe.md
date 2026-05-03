# CleverCache.EntityFrameworkCore

EF Core integration for [CleverCache](https://www.nuget.org/packages/CleverCache) — automatic cache invalidation via `SaveChangesInterceptor` and optional DbSet navigation scanning.

## Setup

```csharp
// Register CleverCache core
builder.Services.AddCleverCache();

// Register EF Core integration (interceptor only)
builder.Services.AddCleverCacheEntityFramework();

// Or with automatic DbSet navigation scanning
builder.Services.AddCleverCacheEntityFramework<AppDbContext>(o =>
    o.NavigationScanMode = DependentCacheNavigationScanMode.Direct);
```

See the [Getting Started](https://github.com/chunty/CleverCache/wiki/Getting-Started) wiki for full setup instructions.
