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

### Multiple types

```csharp
[AutoCache([typeof(Order), typeof(Customer)])]
public record GetOrderSummaryQuery(int OrderId) : IRequest<OrderSummary>;
```

The cache entry is evicted when *either* type changes.

### Entry options

```csharp
[AutoCache([typeof(Order)], AbsoluteExpirationSeconds = 300)]
public record GetOrdersQuery(int CustomerId) : IRequest<List<Order>>;
```

### Respects registered dependencies

`[AutoCache]` and `[InvalidatesCache]` both work through the same dependency tree as the rest of CleverCache. If you have `[DependentCaches]` configured (or cascades registered via `ScanAssemblyContaining`), you only need to reference the root type — dependents are handled automatically.

For example, if `Order` is configured to cascade to `OrderLine` and `OrderNote`, this is sufficient:

```csharp
[AutoCache([typeof(Order)])]
public record GetOrdersQuery(int CustomerId) : IRequest<List<Order>>;

[InvalidatesCache(typeof(Order))]
public record DeleteOrderCommand(int OrderId) : IRequest;
```

You do not need to list `OrderLine` or `OrderNote` — invalidating `Order` cascades to them automatically.

---

Add `[InvalidatesCache]` to any command to automatically clear the specified cache types after the handler completes successfully:

```csharp
[InvalidatesCache(typeof(Order), typeof(OrderLine))]
public record DeleteOrderCommand(int OrderId) : IRequest;
```

- Cache is only cleared if the handler **completes without throwing** — a failed command leaves the cache untouched.
- Works with any return type (`IRequest`, `IRequest<T>`, etc.).
- Types are cleared in the order declared; each call to `RemoveByType` also cascades to dependent types (see [Dependent Caches](Dependent-Caches)).

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
