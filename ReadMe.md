CleverCache
====================================================
[![NuGet](https://img.shields.io/nuget/dt/clevercache.svg)](https://www.nuget.org/packages/clevercache) 
[![NuGet](https://img.shields.io/nuget/vpre/clevercache.svg)](https://www.nuget.org/packages/clevercache)

**CleverCache** was designed to try and solve the problem having to remember (or know when) to invalidate cache entries
when the data in them is out of date. This often particularly hard when cache entries contain data from multiple entities
a change in any of them effectively means the cached data is now wrong. Trying to do this manually often causes 
cross-cutting concerns and invariably we forget something important which proves to be a right pain in the butt.

With a small amount of configuration **CleverCache** will automatically track changes in your database context
and reset the cache for any entity if an entity of that type is create, updated or deleted, and - if required, 
any related entity where data is also part of the same cache entry.

## 🚀 MediatR users

Install [`CleverCache.MediatR`](https://www.nuget.org/packages/clevercache.mediatr)
for **automatic query caching with zero changes to your handlers** — just add `[AutoCache]` to your
query class and CleverCache handles the rest, including automatic invalidation when your data changes.

[Jump to MediatR docs ↓](#auto-caching-mediatr-queries)

> **Automatic invalidation requires Entity Framework Core.** CleverCache hooks into EF Core as a
> `SaveChangesInterceptor` — cache entries are cleared automatically when `SaveChanges` or
> `SaveChangesAsync` completes. If you write data outside EF Core's change tracker (raw SQL via
> `ExecuteUpdate`/`ExecuteDelete`, stored procedures, or external services) those writes won't
> trigger automatic invalidation — call `cache.RemoveByType<T>()` manually instead.
>
> **No EF Core at all?** You can still use CleverCache for manual invalidation. `RemoveByType<T>()`
> understands your dependent-cache tree, so one call handles all the cascades — you just trigger it
> yourself rather than it happening automatically on save.

## Installing CleverCache
You should install CleverCache with NuGet:
```
Install-Package CleverCache
```
Or via the .NET Core command line interface:
```
dotnet add package CleverCache
```
Either commands, from Package Manager Console or .NET Core CLI, will download and install 
CleverCache and all required dependencies.

## Cache provider

By default CleverCache uses `IMemoryCache`. You can switch to a distributed cache, use the dedicated Redis package, or plug in your own provider:

```csharp
// Memory cache (default)
builder.Services.AddCleverCache();

// Redis — install CleverCache.Redis, then:
builder.Services.AddCleverCache(o => o.UseRedisCache("localhost:6379"));

// Any IDistributedCache backend — register it first, then:
builder.Services.AddDistributedMemoryCache(); // or AddStackExchangeRedisCache, etc.
builder.Services.AddCleverCache(o => o.UseDistributedCache());

// Custom provider — implement ICleverCacheStore
builder.Services.AddCleverCache(o => o.UseCustomStore<MyStore>());
// or via a factory:
builder.Services.AddCleverCache(o => o.UseCustomStore(sp => new MyStore(sp.GetRequiredService<IFoo>())));
```

### Cache entry options

All `GetOrCreate` / `GetOrCreateAsync` overloads accept an optional `CleverCacheEntryOptions`:

```csharp
var options = new CleverCacheEntryOptions
{
    // Expire 10 minutes after the entry was created
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),

    // Or expire at a specific point in time
    AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1),

    // Extend lifetime on each read (memory cache only)
    SlidingExpiration = TimeSpan.FromMinutes(5),
};

var result = await cache.GetOrCreateAsync<MyEntity, List<Result>>(
    key,
    async () => await db.Results.ToListAsync(),
    options);
```

## Get Started

1. Register the services:
    ```csharp
    builder.Services.AddCleverCache();
    ```

2. Ensure the interceptor is registered on your database context in any of the following ways:
    ```csharp
    // The interceptor interface if you have no other interceptors
    public class AppDbContext(IInterceptor cleverCacheInterceptor) : DbContext()
    {
        private readonly IInterceptor _cleverCacheInterceptor = cleverCacheInterceptor;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(new IInterceptor[] { cleverCacheInterceptor });
        }
    }
    ```
    or 

    ```csharp
    // The interceptor array if you are already using interceptors
    public class AppDbContext(IInterceptor[] interceptors) : DbContext()
    {
        private readonly IInterceptor[] _interceptors= interceptors;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }
    }
    ```

    or the concrete class

    ```csharp
    // The interceptor interface if you have no other interceptors
    public class AppDbContext(CleverCacheInterceptor cleverCacheInterceptor) : DbContext()
    {
        private readonly CleverCacheInterceptor _cleverCacheInterceptor = cleverCacheInterceptor;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(new IInterceptor[] { cleverCacheInterceptor });
        }
    }
    ```
3. Add the using to your app specifying the db context you are tracking:

    ```csharp
    app.UseCleverCache<AppDbContext>();
    ```

## Usage
You create cache entries in the same way you would with MemoryCache, but specify an additional type parameter to associate a given type with a cache key:
```csharp
// Generic shorthand — associate with a single type
var myItems = await cache.GetOrCreateAsync<MyEntityType, List<MyItem>>(
    cacheKey,
    async () => await db.MyItems.ToListAsync()
) ?? [];
```

The interceptor tracks when any instance of `MyEntityType` is added, changed or deleted and clears all cache keys associated with that type.

You can also supply cache entry options:
```csharp
var myItems = await cache.GetOrCreateAsync<MyEntityType, List<MyItem>>(
    cacheKey,
    async () => await db.MyItems.ToListAsync(),
    new CleverCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) }
) ?? [];
```

## Dependent Caches
Often you have information in a cache entry that contains data from multiple entity types 
and the caches needs to be refreshed if ANY of the types changes not
just the primary object.

> tl;dr: See the `DependantCaches` attribute below

You can create these associations manually on a type by type basis by calling:
```csharp
cache.AddKeyToType(type, key);
```
or more succinctly:
```csharp
cache.AddKeyToType<OtherType>(key);
```

You can also do multiple types in one call by doing:
```csharp
cache.AddKeyToTypes(arrayOfTypes, key);
```

You can also do it by specifying an array of types when calling any of the create methods.

However, this can be tiresome and result in repetitive code. If you know 
you often need to do this for a given entity you can configure it globally via
an attribute on the entity class like this:

```csharp
[DependantCaches([typeof(ThingTwo),typeof(ThingThree)])]
public class ThingOne 
{
    public ThingTwo Two {get; set;};
    public ThingThree Three {get; set;};
}

public class ThingTwo;
public class ThingThree;
```
This will automatically register any keys for `ThingOne` with `ThingTwo` and `ThingThree` 
so changes to any object of these types will clear the cache key. You can also reverse these
mappings by using `reverse: true` in the attribute. This will register `ThingTwo` and `ThingThree` with `ThingOne`

## Auto caching MediatR queries
This is available via the separate **[CleverCache.MediatR](https://www.nuget.org/packages/clevercache.mediatr)** package — install it to keep your main project free of the MediatR dependency.

```
Install-Package CleverCache.MediatR
```

Add the following to your MediatR setup:

```csharp
services.AddMediatR(cfg =>
{
	// Other config you may have
	cfg.AddCleverCache(); // Registers the mediatr pipeline behaviour
});
```
Then simply add the following attribute to any query you want to cache, specifing the type(s) 
you want the cache for:
```csharp
[AutoCache([typeof(MyEntityType)])]
public record MyQuery : IRequest;
```
This uses the mediatr request as the cache key so you can use the same query with different parameters 
and it will cache each one separately.

### Auto-invalidating on MediatR commands

Add `[InvalidatesCache]` to any command to automatically clear the specified cache types after the
command handler completes successfully:

```csharp
[InvalidatesCache(typeof(Order), typeof(OrderLine))]
public record DeleteOrderCommand(int OrderId) : IRequest;
```

Cache is only cleared if the handler completes without throwing — a failed command leaves the cache untouched.

## Redis cache

Install the dedicated **[CleverCache.Redis](https://www.nuget.org/packages/clevercache.redis)** package to add Redis support without bringing the StackExchange.Redis dependency into your main project.

```
Install-Package CleverCache.Redis
```

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

## Custom cache store

Implement `ICleverCacheStore` to plug in any backing store:

```csharp
public interface ICleverCacheStore
{
    bool TryGet<TItem>(object key, out TItem? value);
    Task<(bool Hit, TItem? Value)> TryGetAsync<TItem>(object key, CancellationToken cancellationToken = default);
    void Set<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null);
    Task SetAsync<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default);
    void Remove(object key);
    Task RemoveAsync(object key, CancellationToken cancellationToken = default);
}
```

Example — a simple in-memory dictionary store:

```csharp
public class DictionaryCacheStore : ICleverCacheStore
{
    private readonly Dictionary<string, object?> _store = new();

    private static string Key<TItem>(object key) => $"{typeof(TItem).FullName}:{key}";

    public bool TryGet<TItem>(object key, out TItem? value)
    {
        if (_store.TryGetValue(Key<TItem>(key), out var hit))
        {
            value = (TItem?)hit;
            return true;
        }
        value = default;
        return false;
    }

    public Task<(bool Hit, TItem? Value)> TryGetAsync<TItem>(object key, CancellationToken cancellationToken = default)
    {
        var found = TryGet<TItem>(key, out var value);
        return Task.FromResult((found, value));
    }

    public void Set<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null)
        => _store[Key<TItem>(key)] = value;

    public Task SetAsync<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Remove(object key) => _store.Remove(Key<object>(key));

    public Task RemoveAsync(object key, CancellationToken cancellationToken = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }
}
```

Register it with:

```csharp
builder.Services.AddCleverCache(o => o.UseCustomStore<DictionaryCacheStore>());
// or via a factory:
builder.Services.AddCleverCache(o => o.UseCustomStore(sp => new DictionaryCacheStore()));
```

## Bulk operations and non-EF writes

EF Core's `ExecuteDelete` and `ExecuteUpdate` (and any other writes that bypass the change tracker —
stored procedures, raw SQL, external services) do **not** trigger the `SaveChangesInterceptor`, so
CleverCache won't automatically invalidate affected entries. Two workarounds are available:

### Option 1 — Fluent `.InvalidateCaches()` (any project)

Chain `.InvalidateCaches()` after any operation that returns `int` (rows affected):

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

The call passes through the row count so existing code that uses the return value still compiles.

### Option 2 — `[InvalidatesCache]` on MediatR commands (CleverCache.MediatR)

If you're using CQRS with MediatR, decorate the command instead — no manual cache calls needed:

```csharp
[InvalidatesCache(typeof(Order), typeof(OrderLine))]
public record DeleteOrderCommand(int OrderId) : IRequest;
```

See the [MediatR section ↑](#auto-caching-mediatr-queries) for setup.

## Unit testing
Unit testing methods that use cache is generally fiddly. To help with this, **CleverCache** ships with a
`FakeCache` implementation. It never caches and always calls your underlying factory, so the cache is
completely transparent in your tests.

```csharp
var mocker = new AutoMocker();
mocker.Use<ICleverCache>(new FakeCache());
var sut = mocker.CreateInstance<CarServiceWithCache>();

// Run unit tests as normal
var result = sut.GetDoorCount();
```
Now you can unit test the `GetDoorCount` method without the cache getting in the way.

If you need to **verify cache interactions** (e.g. assert the factory was called exactly once, or that
a specific key was used), use `Mock<ICleverCache>` directly instead — `ICleverCache` is a plain interface
and works with any mocking library.

> **Note:** If you are *only* using the MediatR automatic caching (`[AutoCache]`) and never injecting
> `ICleverCache` into your own services, you don't need `FakeCache` at all.
