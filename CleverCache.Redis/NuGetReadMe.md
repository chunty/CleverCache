# CleverCache.Redis

Redis integration for [CleverCache](https://www.nuget.org/packages/CleverCache) — adds a `UseRedisCache()` convenience method backed by `StackExchange.Redis`.

For full documentation see the [Cache Providers wiki](https://github.com/chunty/CleverCache/wiki/Cache-Providers).

## Quick start

```csharp
// Simple connection string
builder.Services.AddCleverCache(o => o.UseRedisCache("localhost:6379"));

// Full Redis options
builder.Services.AddCleverCache(o => o.UseRedisCache(redis =>
{
    redis.Configuration = "localhost:6379";
    redis.InstanceName = "MyApp:";
}));
```
