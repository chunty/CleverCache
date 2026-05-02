# CleverCache.MediatR

MediatR pipeline integration for [CleverCache](https://www.nuget.org/packages/CleverCache) — automatically caches MediatR query results using the `[AutoCache]` attribute with zero changes to your handlers.

For full documentation see the [GitHub repository](https://github.com/chunty/CleverCache).

## Quick start

```csharp
// 1. Register the pipeline behaviour
services.AddMediatR(cfg =>
{
    cfg.AddCleverCache();
});

// 2. Decorate any query with [AutoCache]
[AutoCache([typeof(MyEntity)])]
public record GetMyQuery(int Id) : IRequest<MyEntity>;
```
