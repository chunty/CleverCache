using CleverCache.Implementations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CleverCache.Tests;

public class DistributedCacheStoreTests
{
    private static DistributedCacheStore CreateStore()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new DistributedCacheStore(cache);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var store = CreateStore();

        var found = store.TryGet<string>("missing", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsDeserializedValue()
    {
        var store = CreateStore();

        store.Set("key1", "distributed-hello");
        var found = store.TryGet<string>("key1", out var value);

        Assert.True(found);
        Assert.Equal("distributed-hello", value);
    }

    [Fact]
    public void Remove_AfterSet_TryGetReturnsFalse()
    {
        var store = CreateStore();
        store.Set("key1", 42);

        store.Remove("key1");

        Assert.False(store.TryGet<int>("key1", out _));
    }

    [Fact]
    public async Task SetAsync_ThenTryGetAsync_ReturnsValue()
    {
        var store = CreateStore();

        await store.SetAsync("key1", "async-value", cancellationToken: TestContext.Current.CancellationToken);
        var (found, value) = await store.TryGetAsync<string>("key1", TestContext.Current.CancellationToken);

        Assert.True(found);
        Assert.Equal("async-value", value);
    }

    [Fact]
    public async Task RemoveAsync_AfterSetAsync_TryGetReturnsFalse()
    {
        var store = CreateStore();
        await store.SetAsync("key1", 99, cancellationToken: TestContext.Current.CancellationToken);

        await store.RemoveAsync("key1", TestContext.Current.CancellationToken);

        var (found, _) = await store.TryGetAsync<int>("key1", TestContext.Current.CancellationToken);
        Assert.False(found);
    }
}
