# Caching Data

Inject `ICleverCache` into your service and use `GetOrCreate` or `GetOrCreateAsync` — same pattern as `IMemoryCache`, with an extra type parameter that tells CleverCache which entity type owns this cache entry.

## Basic usage

```csharp
public class OrderService(ICleverCache cache, AppDbContext db)
{
    public async Task<List<Order>> GetAllAsync()
        => await cache.GetOrCreateAsync<Order, List<Order>>(
               "orders-all",
               async () => await db.Orders.ToListAsync()
           ) ?? [];
}
```

When any `Order` is saved (created, updated, or deleted) via EF Core, the `"orders-all"` entry is automatically evicted. The same applies if a type that `Order` is registered as a dependent of is saved — for example, if `Customer` is configured to cascade to `Order`, saving a `Customer` will also evict `"orders-all"`.

> [!WARNING]
> ⚠️ **WARNING:** If you pass a complex object as the cache key, CleverCache will try to canonicalize it structurally. That is usually fine for simple request objects, but types with expressions, deferred iterators, or other unusual members can produce noisy keys. For those cases, use a type-specific cache key provider (see [Cache Key Providers](Cache-Key-Providers)). If CleverCache still cannot produce a stable key, it will log a warning and skip caching for that call.

## Multiple types

If a cache entry contains data from more than one entity type, pass all relevant types — the entry will be evicted when *any* of them changes:

```csharp
var summary = await cache.GetOrCreateAsync<List<OrderSummary>>(
    new[] { typeof(Order), typeof(Customer) },
    "order-summary",
    async () => await BuildSummaryAsync()
);
```

## Entry options

All overloads accept an optional `CleverCacheEntryOptions`. If you do not pass one, CleverCache uses a 4 hour sliding expiration by default:

```csharp
var options = new CleverCacheEntryOptions
{
    // Expire 10 minutes after creation
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),

    // Or expire at a fixed point in time
    AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1),

    // Extend lifetime on each read (memory cache only)
    SlidingExpiration = TimeSpan.FromMinutes(5),
};

var result = await cache.GetOrCreateAsync<Order, List<Order>>(
    "orders-all",
    async () => await db.Orders.ToListAsync(),
    options
);
```

The default can be changed globally by setting `CleverCacheOptions.DefaultEntryOptions` during `AddCleverCache(...)`.

## Manual removal

Remove a specific key:

```csharp
cache.Remove("orders-all");
```

Remove all entries for a type (also cascades to dependent types — see [Dependent Caches](Dependent-Caches)):

```csharp
cache.RemoveByType<Order>();
// or
cache.RemoveByType(typeof(Order));
```

## Concurrent fills and invalidation

Invalidation prevents a factory already running for a tracked key from publishing its pre-invalidation result. The original caller can still receive that result; CleverCache does not retry the query. A subsequent cache miss runs a new factory.

This applies to both synchronous and asynchronous APIs, whether `EnableAsyncRaceConditionGuard` is enabled or disabled. That option only controls whether competing same-key misses share one factory execution.

Cache hits do not acquire invalidation locks. Publication and removal are coordinated per key, including asynchronous store writes. Invalidation never waits for the query factory, although it can wait for a store operation already publishing that key. Operations on unrelated keys remain independent.

Tracked registrations are retained until an invalidated in-flight fill finishes, so diagnostics can temporarily include a pending key without a stored value. Failed factories clean up unused registrations. If a store write reports an error, registrations are conservatively retained because the store may have written before throwing; the exception propagates and a subsequent successful removal clears the registrations.

These guarantees apply within one CleverCache instance. They do not provide cross-server invalidation or defer EF invalidation until an outer transaction commits.
