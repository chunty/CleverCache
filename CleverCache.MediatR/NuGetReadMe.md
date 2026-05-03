# CleverCache.MediatR

MediatR pipeline integration for [CleverCache](https://www.nuget.org/packages/CleverCache) — automatically caches MediatR query results and invalidates cache on commands, with zero changes to your handlers.

For full documentation see the [GitHub repository](https://github.com/chunty/CleverCache).

## Quick start

```csharp
// 1. Register the pipeline behaviours
services.AddMediatR(cfg =>
{
    cfg.AddCleverCache();
});

// 2. Cache query results — decorate any query with [AutoCache]
[AutoCache([typeof(MyEntity)])]
public record GetMyQuery(int Id) : IRequest<MyEntity>;

// 3. Invalidate on commands — decorate any command with [InvalidatesCache]
[InvalidatesCache(typeof(MyEntity))]
public record DeleteMyCommand(int Id) : IRequest;
```

Cache is cleared after the command handler completes successfully. A failed handler leaves the cache untouched.
