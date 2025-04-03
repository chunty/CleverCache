# Setup

1. Add the services:
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

# Usage
You create cache in the same way you would when using MemoryCache but specify an additional type parameter like this to associate a given type with a cache key:
```csharp
    var myItem = await cache.GetOrCreateAsync(
	    typeof(MyEntityType),
	    cacheKey,
	    _ => 
	    {
		    //return <Do real query for data>;
	    }
    ) ?? [];
```

The interceptor tracks when any instance of MyEntityType is added, changed or deleted and will clear all cache keys associated with that type

# Dependent Caches
Often you have information in a cache entry that contains data from multiple entity types 
and the caches needs to be refreshed if ANY of the types changes not
just the primary object.

You can create these association manually on a type by type basis by calling:
```csharp
cache.AddKeyToType(type, key);
```
or more succinctly:
```csharp
cache.AddKeyToType<OtherType>(key);
```

You can also do multiple types in one call by calling:
```csharp
cache.AddKeyToTypes(arrayOfTypes, key);
```

You can also do it by specifying an array of types when calling any of the create methods.

However this can be tiresome and result in repetitive code. If you know 
you often need to do this for a given entity you configure it globally via
an attribute on the class like this:

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
mapping by using `reverse: true` in the attribute. This will register `ThingTwo` and `ThingThree` with `ThingOne`

# Auto caching mediatr queries
This is really powerful tool that enables you to quickly add caching to your mediatr queries without any changes 
to your handlers.

Add the following to your mediatr setup:

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
```
This uses the mediatr request as the cache key so you can use the same query with different parameters 
and it will cache each one separately.
