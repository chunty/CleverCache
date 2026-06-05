# Cache Key Providers

Use `ICacheKeyProvider<T>` when a request object needs a stable cache key shape that is different from its default structural form.

## Why this exists

Most request objects can be cached directly, but complex members such as expressions, iterators, or other non-ideal object graphs can produce noisy or unstable keys. A type-specific provider lets you reduce the key to the fields that actually define cache identity.

Use this when the default object-based key is too broad, too noisy, or not obviously stable across runs. Typical cases are:

- queries that contain `Expression` trees
- requests that carry deferred `IEnumerable` values
- objects with members that are only useful for execution, not identity
- requests where only a subset of fields should affect cache identity

Don’t use it for simple request records that already map cleanly to a stable structural key; the default path is simpler and usually good enough there.

## Register a provider

```csharp
public sealed class GetLeadSourceBrandsQueryKeyProvider
    : ICacheKeyProvider<GetLeadSourceBrandsQuery>
{
    public object GetKey(GetLeadSourceBrandsQuery value)
        => new { value.LeadSourceId };
}

builder.Services.AddCleverCache(o =>
    o.AddKeyProvider<GetLeadSourceBrandsQuery, GetLeadSourceBrandsQueryKeyProvider>());
```

## Scan for providers

If you prefer discovery over explicit registration, use:

```csharp
builder.Services.AddCleverCache(o =>
    o.ScanKeyProviderAssemblies<GetLeadSourceBrandsQuery>());
```

or:

```csharp
builder.Services.AddCleverCache(o =>
    o.ScanKeyProviderAssemblies(typeof(GetLeadSourceBrandsQuery).Assembly));
```

## Notes

- Providers are resolved by runtime type and cached in memory.
- Keep the returned key stable and deterministic.
- For simple request shapes, the default structural key generation is usually enough.
- If CleverCache still cannot produce a stable key, it logs a warning and skips caching for that call rather than risking a bad cache entry.
