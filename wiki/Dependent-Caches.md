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

## Scenario 2 — Invalidating one type should always cascade to another

If you always want invalidating `Order` to also invalidate `OrderLine` caches — regardless of how or where that invalidation is triggered — register a cascade rule.

**Via the `[DependentCaches]` attribute** (recommended — configured once at startup):

```csharp
[DependentCaches([typeof(OrderLine), typeof(OrderNote)])]
public class Order { }
```

Register the assembly containing your entities when configuring CleverCache — no EF Core required:

```csharp
builder.Services.AddCleverCache(o => o.ScanAssemblyContaining<Order>());
// or, when using EF Core:
builder.Services.AddCleverCacheEntityFramework(o => o.ScanAssemblyContaining<Order>());
```

CleverCache scans the assembly at startup and registers the cascade rules. Now `cache.RemoveByType<Order>()` also clears all `OrderLine` and `OrderNote` entries automatically.

**Programmatically** (for dynamic or test scenarios):

```csharp
cache.AddDependentCache<Order>(typeof(OrderLine));
```

### Cascades are transitive

`A → B → C` means removing `A` also removes `B` and `C`. Cycles are handled safely.

### Reverse mappings

Use `reverse: true` to also register the inverse — so invalidating `OrderLine` cascades back to `Order`:

```csharp
[DependentCaches([typeof(OrderLine)], reverse: true)]
public class Order { }
```

---

## Scenario 3 — Auto-wire the whole context via navigation scanning

If you want cascade rules discovered automatically from your EF Core model without decorating any classes, call `ScanDbSetsForCacheDependencies` after building your app. This inspects every entity type in the context's model and registers cascades based on navigation properties:

```csharp
app.ScanDbSetsForCacheDependencies<AppDbContext>();

// Control the depth of scanning:
app.ScanDbSetsForCacheDependencies<AppDbContext>(o =>
    o.NavigationScanMode = DependentCacheNavigationScanMode.Direct);

// Also register reverse cascades (when OrderLine changes, also clear Order):
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

> **⚠️ Consider carefully before using this in large projects.** Global navigation scanning wires up every entity in your context. In a large schema this can create a very wide dependency tree — a change to a central entity like `Customer` or `User` may cascade to dozens of cache keys, leading to excessive invalidation and high memory usage from tracking all those key associations. For most projects, Scenario 2 (`[DependentCaches]` attributes with `ScanAssemblyContaining`) gives you the same convenience with precise, opt-in control over which relationships matter.

> **Navigation scanning and attributes are independent.** `ScanDbSetsForCacheDependencies` discovers relationships purely from EF navigation properties. `[DependentCaches]` attributes are registered separately via `ScanAssemblyContaining`. Use both together for full coverage.

