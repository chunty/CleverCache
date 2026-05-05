# Diagnostics

CleverCache can produce a snapshot of its internal state at any point — useful for debugging misconfigured dependency trees or unexpectedly broad cache invalidation.

---

## `GetDiagnostics()`

Returns a `CleverCacheDiagnostics` record with two properties:

| Property | Type | Description |
|---|---|---|
| `Dependants` | `IReadOnlyDictionary<Type, IReadOnlyList<Type>>` | Direct cascade edges — the types that will also be invalidated when a given type changes |
| `KeysByType` | `IReadOnlyDictionary<Type, IReadOnlyList<object>>` | All cache keys currently tracked under each type, including keys inherited via cascade expansion |

```csharp
var diagnostics = cache.GetDiagnostics();

// Which types does invalidating Order cascade to?
var cascades = diagnostics.Dependants[typeof(Order)];

// Which keys are currently tracked under Order?
var keys = diagnostics.KeysByType[typeof(Order)];
```

---

## `RenderDependencyTree()`

Formats the full dependency graph as a human-readable string. Useful for logging at startup or exposing via a diagnostic endpoint.

```csharp
// Log at startup
logger.LogDebug(cache.RenderDependencyTree());

// Expose via a diagnostic endpoint
app.MapGet("/_cache/tree", (ICleverCache cache) => cache.RenderDependencyTree());
```

Example output:

```
CleverCache Dependency Tree
────────────────────────────────────────
Customer
  ↳ cascades to : Order
Order
  ↳ cascades to : OrderLine, OrderNote
  ↳ tracked keys : orders-all, order:123, order:456
OrderLine
  ↳ tracked keys : orderlines-for-123
OrderNote
─ 4 type(s) | 2 cascade rule(s) | 4 tracked key(s) ─
```

> **Note:** `KeysByType` reflects the keys currently tracked by CleverCache. Keys are removed from the tracking set when:
> - `Remove(key)` is called explicitly
> - `RemoveByType(type)` is called (e.g. via the EF Core interceptor on `SaveChanges`)
> - The entry is evicted from the underlying store and the store implements `IEvictionNotifyingStore` (e.g. `MemoryCacheStore`)
>
> For distributed cache stores that don't support eviction notifications, keys may remain visible in `KeysByType` after their TTL expires in the remote store. This does not affect correctness — stale keys are simply no-ops when invalidation fires.
