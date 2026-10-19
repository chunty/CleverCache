# Migrating from V2 to V3

V3 clarifies the names used to describe cache invalidation dependencies. Dependency direction and invalidation behavior are unchanged.

For changes introduced in V2, see [Migrating to V2](Migrating-to-V2).

## Summary

| Area | V2 | V3 |
|---|---|---|
| Entity dependency attribute | `DependentCaches` | `CacheInvalidatedBy`; the old attribute remains an obsolete compatibility alias |
| Entity dependency constructor | `Type[] types, bool reverse = false` | `params Type[] types`; configure `Reverse` as a named property |
| Multiple entity dependency declarations | One attribute per class | Multiple `CacheInvalidatedBy` instances, each with its own `Reverse` setting |
| `AutoCache` constructor parameter | `types` | `invalidatedBy` |
| `AutoCache` dependency property | `Types` | `InvalidatedBy`; `Types` remains an obsolete compatibility alias |

## 1. Replace `DependentCaches` with `CacheInvalidatedBy`

Before:

```csharp
[DependentCaches([typeof(OrderLine), typeof(OrderNote)], reverse: true)]
public class Order { }
```

After:

```csharp
[CacheInvalidatedBy(typeof(OrderLine), typeof(OrderNote), Reverse = true)]
public class Order { }
```

The new constructor accepts individual types or an array. Change the old `reverse: true` constructor argument to the named property `Reverse = true`.

Changes to a listed type invalidate the decorated entity's cache. `Reverse = true` also registers the opposite direction for every type in that attribute instance. Dependencies remain transitive and cycle-safe.

The old `DependentCaches` attribute inherits `CacheInvalidatedBy` and retains its original constructor and single-instance restriction, but now produces an obsolete warning. Projects that treat warnings as errors must migrate these usages to build without that warning.

## 2. Use separate declarations for different reverse settings

```csharp
[CacheInvalidatedBy(typeof(OrderLine), Reverse = true)]
[CacheInvalidatedBy(typeof(OrderNote))]
public class Order { }
```

Both `OrderLine` and `OrderNote` changes invalidate `Order` caches. Invalidating `Order` also invalidates `OrderLine`, but not `OrderNote`. Because dependencies are transitive, invalidating `OrderNote` also invalidates `OrderLine` through `Order`.

Continue registering attributes with `ScanAssemblyContaining<T>()`; no startup change is required:

```csharp
builder.Services.AddCleverCacheEntityFramework(o => o.ScanAssemblyContaining<Order>());
```

## 3. Update named `AutoCache` arguments and property access

The types supplied to `AutoCache` identify invalidation dependencies, not the types returned by the query.

Positional usage is unchanged:

```csharp
[AutoCache(typeof(Order), typeof(Customer))]
public record GetOrderSummaryQuery(int OrderId) : IRequest<OrderSummary>;
```

For named constructor arguments, replace `types:` with `invalidatedBy:`:

```csharp
[AutoCache(invalidatedBy: new[] { typeof(Order), typeof(Customer) })]
public record GetOrderSummaryQuery(int OrderId) : IRequest<OrderSummary>;
```

Replace code reading `AutoCacheAttribute.Types` with `AutoCacheAttribute.InvalidatedBy`. The old property remains an obsolete alias returning the same array. The constructor parameter rename is a source-breaking change for named arguments; existing positional calls and the constructor's binary signature are unchanged.

The pipeline uses `InvalidatedBy`. Invalidating either listed type evicts the cached result, including through registered dependency cascades. Expiration settings and cache keys are unchanged.
