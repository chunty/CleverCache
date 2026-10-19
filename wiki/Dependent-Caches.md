# Dependent Caches

CleverCache tracks which entity types a cache entry is associated with and evicts it automatically when those types change. This page covers the scenarios for configuring cache dependency relationships.

---

## Scenario 1 — A cache entry spans multiple entity types

If a single cache entry contains data from more than one entity type, pass all relevant types when creating it. The entry is evicted when *any* of them changes:

```csharp
var summary = await cache.GetOrCreateAsync<List<OrderSummary>>(
    new[] { typeof(Order), typeof(Customer) },
    "order-summary",
    async () => await BuildSummaryAsync()
);
```

For keys that weren't created through `GetOrCreate` — for example after a bulk operation — register them after the fact:

```csharp
cache.AddKeyToType<Order>("orders-all");
cache.AddKeyToTypes(new[] { typeof(Order), typeof(Customer) }, "order-summary");
```

---

## Scenario 2 — One entity's cached data depends on other types

If cached `Order` data depends on `OrderLine` and `OrderNote`, declare those dependencies on `Order`. Invalidating either listed type also invalidates `Order` caches, regardless of how or where invalidation is triggered.

**Via the `[CacheInvalidatedBy]` attribute** (recommended — configured once at startup):

```csharp
[CacheInvalidatedBy(typeof(OrderLine), typeof(OrderNote))]
public class Order { }
```

Register the assembly containing your entities when configuring CleverCache — no EF Core required:

```csharp
builder.Services.AddCleverCache(o => o.ScanAssemblyContaining<Order>());
// or, when using EF Core:
builder.Services.AddCleverCacheEntityFramework(o => o.ScanAssemblyContaining<Order>());
```

CleverCache scans the assembly at startup and registers the dependencies. Now `cache.RemoveByType<OrderLine>()` or `cache.RemoveByType<OrderNote>()` also clears `Order` entries automatically. Invalidating `Order` alone does not clear `OrderLine` or `OrderNote` entries.

`[CacheInvalidatedBy]` accepts `params Type[]`, so you can pass types individually or as an array. Set the optional reverse relationship with the named property `Reverse = true`.

`[DependentCaches]` is obsolete but remains supported with its original `(Type[] types, bool reverse = false)` constructor. When migrating, pass the types to `[CacheInvalidatedBy]` and replace any `reverse: true` constructor argument with `Reverse = true`. Dependency direction is unchanged. Existing source receives an obsolete warning, which may require migration or a targeted suppression in projects treating warnings as errors.

**Programmatically** (for dynamic or test scenarios):

```csharp
cache.AddDependentCache<Order>(typeof(OrderLine));
```

### Dependencies are transitive

Read `A → B` as "A's cached data depends on B." With `A → B → C`, invalidating `C` clears caches for `C`, `B`, and `A`. Invalidating `A` alone does not clear `B` or `C` caches. Cycles are handled safely.

### Reverse mappings

Use `Reverse = true` to make the relationship bidirectional. `Order` depends on `OrderLine`, and `OrderLine` also depends on `Order`. Declare this on one entity instead of adding matching attributes to both:

```csharp
[CacheInvalidatedBy(typeof(OrderLine), Reverse = true)]
public class Order { }
```

Invalidating either type now clears caches for both.

You can apply multiple `[CacheInvalidatedBy]` instances to give each group its own `Reverse` setting:

```csharp
[CacheInvalidatedBy(typeof(OrderLine), Reverse = true)]
[CacheInvalidatedBy(typeof(OrderNote))]
public class Order { }
```

Changes to either `OrderLine` or `OrderNote` invalidate `Order` caches. Changes to `Order` also invalidate `OrderLine` caches, but not `OrderNote` caches. `Reverse` applies to every type within its own attribute instance, not to other instances. Dependencies remain transitive: changing `OrderNote` also invalidates `OrderLine` caches through their dependency on `Order`.

The obsolete `[DependentCaches]` attribute retains its single-instance usage rule.

---

## Scenario 3 — Auto-wire the whole context via navigation scanning

If you want dependencies discovered automatically from your EF Core model without decorating any classes, call `ScanDbSetsForCacheDependencies` after building your app. This inspects every entity type in the context's model. A navigation from `Order` to `OrderLine` makes `Order` caches depend on `OrderLine`, so changing `OrderLine` invalidates `Order` caches:

```csharp
app.ScanDbSetsForCacheDependencies<AppDbContext>();

// Control the depth of scanning:
app.ScanDbSetsForCacheDependencies<AppDbContext>(o =>
    o.NavigationScanMode = DependentCacheNavigationScanMode.Direct);

// Also register reverse dependencies (Order changes also clear OrderLine caches):
app.ScanDbSetsForCacheDependencies<AppDbContext>(o =>
{
    o.NavigationScanMode = DependentCacheNavigationScanMode.Recursive;
    o.ReverseNavigationDependencies = true;
});
```

| Mode | Behaviour |
|---|---|
| `Direct` (default) | Scans the immediate navigation properties of each entity |
| `Recursive` | Scans the full navigation graph transitively from each entity |
| `None` | No scanning — returns nothing |

> **⚠️ Consider carefully before using this in large projects.** Global navigation scanning wires up every entity in your context. In a large schema this can create a very wide dependency tree — a change to a central entity like `Customer` or `User` may cascade to dozens of cache keys, leading to excessive invalidation and high memory usage from tracking all those key associations. For most projects, Scenario 2 (`[CacheInvalidatedBy]` attributes with `ScanAssemblyContaining`) gives you the same convenience with precise, opt-in control over which relationships matter.

> **Navigation scanning and attributes are independent.** `ScanDbSetsForCacheDependencies` discovers relationships purely from EF navigation properties. `[CacheInvalidatedBy]` attributes are registered separately via `ScanAssemblyContaining`. Use both together for full coverage.
