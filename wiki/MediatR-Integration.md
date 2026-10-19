# MediatR Integration

Install the separate `CleverCache.MediatR` package to keep your main project free of the MediatR dependency:

```
Install-Package CleverCache.MediatR
```

## Setup

Register the CleverCache pipeline behaviours inside your `AddMediatR` call:

```csharp
services.AddMediatR(cfg =>
{
    cfg.AddCleverCache();
});
```

This registers two pipeline behaviours automatically:
- **`InvalidateCacheBehaviour`** — clears cache after a command succeeds (see below)
- **`AutoCacheBehaviour`** — caches query results (see below)

---

## Auto-caching queries with `[AutoCache]`

Add `[AutoCache]` to any MediatR query to cache its result. The MediatR request object itself is used as the cache key, so the same query with different parameters gets its own cache entry.

```csharp
[AutoCache([typeof(Order)])]
public record GetOrdersQuery(int CustomerId) : IRequest<List<Order>>;
```

No changes to the handler — caching is handled entirely by the pipeline behaviour.

When any `Order` is saved via EF Core, all `GetOrdersQuery` cache entries are automatically evicted.

If you do not specify an expiration on `[AutoCache]`, the entry uses the global CleverCache default: a 4 hour sliding expiration. You can change that global default in `AddCleverCache(...)` by setting `DefaultEntryOptions`.

> [!WARNING]
> ⚠️ **WARNING:** The request object is used as the cache key by default. That works well for simple request records, but complex members such as expressions or deferred enumerables can produce awkward keys. If a query needs a custom shape, add a type-specific cache key provider instead of relying on the default object form (see [Cache Key Providers](Cache-Key-Providers)). If CleverCache still cannot produce a stable key, it will log a warning and skip caching for that request.

### Multiple types

```csharp
[AutoCache([typeof(Order), typeof(Customer)])]
public record GetOrderSummaryQuery(int OrderId) : IRequest<OrderSummary>;
```

The `invalidatedBy` constructor parameter and `InvalidatedBy` property identify the types whose invalidation evicts the cached result, not the types returned by the query. The cache entry is evicted when *either* type is invalidated, including through registered dependency cascades.

Positional attribute usage is unchanged. Code using the old named constructor argument `types:` must use `invalidatedBy:` instead. The old `Types` property remains available as an obsolete alias for `InvalidatedBy`.

### Entry options

```csharp
[AutoCache([typeof(Order)], AbsoluteExpirationSeconds = 300)]
public record GetOrdersQuery(int CustomerId) : IRequest<List<Order>>;
```

`[AutoCache]` supports both `SlidingExpirationSeconds` and `AbsoluteExpirationSeconds`. You can set either or both, and they map directly onto `CleverCacheEntryOptions`.

### Respects registered dependencies

`[AutoCache]` and `[InvalidatesCache]` both work through the same dependency tree as the rest of CleverCache. `[AutoCache]` identifies the types whose invalidation clears a query's cached result; `[InvalidatesCache]` identifies the types a command invalidates. Entity `[CacheInvalidatedBy]` declarations extend which changes invalidate those query results.

For example, declare that `Order` caches depend on `OrderLine` and `OrderNote`, and register the entity assembly through `ScanAssemblyContaining<Order>()`:

```csharp
[CacheInvalidatedBy(typeof(OrderLine), typeof(OrderNote))]
public class Order { }

[AutoCache([typeof(Order)])]
public record GetOrdersQuery(int CustomerId) : IRequest<List<Order>>;

[InvalidatesCache(typeof(OrderLine))]
public record DeleteOrderLineCommand(int OrderLineId) : IRequest;
```

Invalidating `OrderLine` also evicts `GetOrdersQuery` entries because `Order` depends on `OrderLine`. You do not need to repeat the entity's dependencies on `[AutoCache]`. Invalidating `Order` alone does not evict caches for `OrderLine` or `OrderNote` unless the reverse relationships are also registered (for example with `Reverse = true`).

---

Add `[InvalidatesCache]` to any command to automatically clear the specified cache types after the handler completes successfully:

```csharp
[InvalidatesCache(typeof(Order), typeof(OrderLine))]
public record DeleteOrderCommand(int OrderId) : IRequest;
```

- Cache is only cleared if the handler **completes without throwing** — a failed command leaves the cache untouched.
- Works with any return type (`IRequest`, `IRequest<T>`, etc.).
- Types are cleared in the order declared; each call to `RemoveByType` also evicts caches whose types depend on the invalidated type (see [Dependent Caches](Dependent-Caches)).

### Combined with `[AutoCache]`

A command decorated with `[InvalidatesCache]` pairs naturally with queries decorated with `[AutoCache]`:

```csharp
// Query — cache results
[AutoCache([typeof(Order)])]
public record GetOrdersQuery(int CustomerId) : IRequest<List<Order>>;

// Command — clear cache after success
[InvalidatesCache(typeof(Order))]
public record CreateOrderCommand(int CustomerId, ...) : IRequest<int>;
```

When `CreateOrderCommand` succeeds, all `GetOrdersQuery` entries for all customers are evicted — no manual cache calls needed anywhere.
