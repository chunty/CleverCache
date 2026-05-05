# Cache Providers

CleverCache supports four cache backend options.

## Memory cache (default)

Uses `IMemoryCache`. No extra configuration needed:

```csharp
builder.Services.AddCleverCache();
```

Supports sliding expiration in addition to absolute expiration. Suitable for single-server deployments.

## Distributed cache

Uses any registered `IDistributedCache` backend (Redis, SQL Server, etc.):

```csharp
// Register your IDistributedCache backend first
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = "localhost:6379");
// or: builder.Services.AddDistributedMemoryCache();

// Then tell CleverCache to use it
builder.Services.AddCleverCache(o => o.UseDistributedCache());
```

> **Note:** Distributed cache does not support sliding expiration.

## Redis (dedicated package)

Install `CleverCache.Redis` for a direct Redis connection independent of `IDistributedCache`:

```
Install-Package CleverCache.Redis
```

> **When to use this instead of the distributed cache option:**
> - You are not using `IDistributedCache` anywhere else in your app and don't want to pull it in just for CleverCache
> - You want CleverCache to use a *different* Redis instance than the one registered as your `IDistributedCache`
>
> If you already have `IDistributedCache` pointing at the right Redis instance, `UseDistributedCache()` is simpler.

```csharp
// Simple connection string
builder.Services.AddCleverCache(o => o.UseRedisCache("localhost:6379"));

// Full options
builder.Services.AddCleverCache(o => o.UseRedisCache(redis =>
{
    redis.Configuration = "localhost:6379";
    redis.InstanceName = "MyApp:";
}));
```

## Custom provider

Implement `ICleverCacheStore` to plug in any backing store:

```csharp
public interface ICleverCacheStore
{
    bool TryGet<TItem>(object key, out TItem? value);
    Task<(bool Hit, TItem? Value)> TryGetAsync<TItem>(object key, CancellationToken cancellationToken = default);
    void Set<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null);
    Task SetAsync<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default);
    void Remove(object key);
    Task RemoveAsync(object key, CancellationToken cancellationToken = default);
}
```

Register it with:

```csharp
builder.Services.AddCleverCache(o => o.UseCustomStore<MyStore>());

// Or via a factory for stores with dependencies:
builder.Services.AddCleverCache(o => o.UseCustomStore(sp => new MyStore(sp.GetRequiredService<IFoo>())));
```

### Eviction notifications (optional)

CleverCache tracks which keys belong to which types in memory. To keep this tracking accurate when entries expire via TTL, your store can implement `IEvictionNotifyingStore` alongside `ICleverCacheStore`:

```csharp
public class MyStore : ICleverCacheStore, IEvictionNotifyingStore
{
    private Action<object>? _onEvicted;

    public void RegisterEvictionCallback(Action<object> onEvicted) => _onEvicted = onEvicted;

    public void Set<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null)
    {
        // ... store the value ...
        // Call _onEvicted(key) whenever this entry is evicted or expires
    }

    // ... rest of ICleverCacheStore implementation
}
```

When `CleverCacheService` detects that your store implements `IEvictionNotifyingStore` at startup, it registers itself as the eviction listener automatically — no additional configuration needed.

If your backing store has no eviction notification API (as is the case with `IDistributedCache`), simply omit `IEvictionNotifyingStore`. Tracked keys are still cleaned up on explicit `Remove`/`RemoveByType` calls; only naturally-expired entries may linger briefly in the in-memory tracking set.

### Example — dictionary-backed store

```csharp
public class DictionaryCacheStore : ICleverCacheStore
{
    private readonly Dictionary<string, object?> _store = new();

    public bool TryGet<TItem>(object key, out TItem? value)
    {
        if (_store.TryGetValue(key.ToString()!, out var hit))
        {
            value = (TItem?)hit;
            return true;
        }
        value = default;
        return false;
    }

    public Task<(bool Hit, TItem? Value)> TryGetAsync<TItem>(object key, CancellationToken cancellationToken = default)
    {
        var found = TryGet<TItem>(key, out var value);
        return Task.FromResult((found, value));
    }

    public void Set<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null)
        => _store[key.ToString()!] = value;

    public Task SetAsync<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Remove(object key) => _store.Remove(key.ToString()!);

    public Task RemoveAsync(object key, CancellationToken cancellationToken = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }
}
```
