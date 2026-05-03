# CleverCache

Automatic cache invalidation for .NET — tracks entity changes via EF Core and clears related cache entries automatically.

Supports **memory cache** (default), **distributed cache** (`IDistributedCache`), or a **custom provider**.

For full documentation, examples, and configuration options see the [CleverCache wiki](https://github.com/chunty/CleverCache/wiki).

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
