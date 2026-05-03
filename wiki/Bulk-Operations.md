# Bulk Operations

EF Core's `ExecuteDelete` and `ExecuteUpdate` — and any other writes that bypass the change tracker (raw SQL, stored procedures, external services) — do **not** trigger CleverCache's `SaveChangesInterceptor`. Cache entries won't be evicted automatically.

Two workarounds are available.

---

## Option 1 — Fluent `.InvalidateCaches()` extension

Chain `.InvalidateCaches()` directly after any operation that returns `int` (rows affected). Works for both sync and async:

```csharp
// Sync
context.Orders.Where(o => o.IsDeleted)
    .ExecuteDelete()
    .InvalidateCaches(cache, typeof(Order));

// Async
await context.Orders.Where(o => o.IsDeleted)
    .ExecuteDeleteAsync()
    .InvalidateCaches(cache, typeof(Order));

// Generic shorthand — no typeof needed
await context.Orders.Where(o => o.IsDeleted)
    .ExecuteDeleteAsync()
    .InvalidateCaches<Order>(cache);

// Works after ExecuteUpdate too
await context.Orders.Where(o => o.Status == "pending")
    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "complete"))
    .InvalidateCaches<Order>(cache);
```

The call passes the row count through as its return value, so existing code that checks how many rows were affected still compiles.

### Multiple types

```csharp
await context.Orders.Where(o => o.IsDeleted)
    .ExecuteDeleteAsync()
    .InvalidateCaches(cache, typeof(Order), typeof(OrderLine));
```

---

## Option 2 — `[InvalidatesCache]` on MediatR commands

If you're using CQRS with MediatR, decorate the command instead — no manual cache calls anywhere in the codebase:

```csharp
[InvalidatesCache(typeof(Order), typeof(OrderLine))]
public record BulkDeleteOrdersCommand(int[] OrderIds) : IRequest<int>;
```

Cache is cleared after the handler completes successfully. A failed handler leaves the cache untouched.

See [MediatR Integration](MediatR-Integration) for setup.

---

## Rolling your own invalidation

If none of the above fit your situation — a message bus consumer, a background job, a third-party SDK that writes directly to the database — you can always call `RemoveByType` directly:

```csharp
public class OrderSyncService(ICleverCache cache)
{
    public async Task SyncFromExternalSystem(IEnumerable<OrderDto> orders)
    {
        // ... write logic ...

        // Manually evict after writes complete
        cache.RemoveByType<Order>();
    }
}
```

`RemoveByType` understands the full dependency tree, so if `Order` is configured to cascade to `OrderLine` and `OrderNote`, a single call handles all three. You can also pass multiple types:

```csharp
cache.RemoveByType<Order>();
cache.RemoveByType<Customer>();
```

Or build a helper that mirrors what `[InvalidatesCache]` does — invalidate a set of types and let the cascades do the rest:

```csharp
private void InvalidateOrderCaches()
{
    cache.RemoveByType<Order>();   // cascades to OrderLine, OrderNote automatically
}
```


The same workarounds apply to any write that bypasses the change tracker — stored procedures, raw SQL via `DbConnection`, or writes from an external service:

```csharp
// After a stored procedure call
await db.Database.ExecuteSqlRawAsync("EXEC sp_ArchiveOrders");
cache.RemoveByType<Order>();

// Or using the extension on the returned task
await db.Database.ExecuteSqlRawAsync("EXEC sp_ArchiveOrders")
    .InvalidateCaches<Order>(cache);
```
