# Dependent Caches

CleverCache tracks which entity types a cache entry is associated with and evicts it automatically when those types change. This page covers four scenarios, from the simplest to the most automatic.

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
```

CleverCache scans the assembly at startup and registers the cascade rules. Now `cache.RemoveByType<Order>()` also clears all `OrderLine` and `OrderNote` entries automatically.

If you are already using `UseCleverCache<AppDbContext>()` for navigation scanning, attribute-based cascades on those same entities are also picked up there — so you don't need `ScanAssemblyContaining` as well.

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

## Scenario 3 — Auto-discover cascades for a specific entity

Instead of listing dependent types by hand, CleverCache can read the EF Core navigation properties on a specific entity and register cascades for you:

```csharp
[DependentCaches([], navigationScanMode: DependentCacheNavigationScanMode.Direct)]
public class Order
{
    public Customer Customer { get; set; }            // → cascade added
    public ICollection<OrderLine> Lines { get; set; } // → cascade added
}
```

| Mode | Behaviour |
|---|---|
| `None` (default) | No navigation scanning — use explicit `types` list only |
| `Direct` | Scans the immediate navigation properties of this entity |
| `Recursive` | Scans the full navigation graph transitively from this entity |

This is useful when you want opt-in, per-entity control — only the entities you decorate are scanned.

---

## Scenario 4 — Auto-wire the whole context with no attributes

If you want every entity in your context wired up automatically without decorating any classes, enable global navigation scanning in `UseCleverCache`:

```csharp
app.UseCleverCache<AppDbContext>(o =>
    o.Scanning.NavigationScanMode = DependentCacheNavigationScanMode.Direct);
```

CleverCache scans the navigation properties on every `DbSet<T>` type in `AppDbContext` and registers the cascades at startup. No `[DependentCaches]` attributes needed anywhere.

```csharp
// Also register reverse cascades (when OrderLine changes, also clear Order)
app.UseCleverCache<AppDbContext>(o =>
{
    o.Scanning.NavigationScanMode = DependentCacheNavigationScanMode.Recursive;
    o.Scanning.ReverseNavigationDependencies = true;
});
```

> **⚠️ Consider carefully before using this in large projects.** Global navigation scanning wires up every entity in your context. In a large schema this can create a very wide dependency tree — a change to a central entity like `Customer` or `User` may cascade to dozens of cache keys, leading to excessive invalidation and high memory usage from tracking all those key associations. For most projects, Scenario 2 (`[DependentCaches]` attributes with `ScanAssemblyContaining`) gives you the same convenience with precise, opt-in control over which relationships matter.

---

## How Scenario 3 and 4 interact

| Global mode | Attribute behaviour |
|---|---|
| `None` (default) | Attributes are processed normally — Scenarios 2 & 3 work as described |
| `Direct` | Global scanning runs first; attributes are **also** processed for any additional explicit types or `reverse` flags |
| `Recursive` | Attribute processing is **skipped entirely** — the full graph is already discovered globally, making per-entity attributes redundant |

> If you use global `Recursive` scanning, `[DependentCaches]` attributes on your entities are ignored. Choose either global scanning or per-entity attributes — don't mix `Recursive` with attribute-based configuration.

