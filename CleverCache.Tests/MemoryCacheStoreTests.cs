using CleverCache.Implementations;
using Microsoft.Extensions.Caching.Memory;

namespace CleverCache.Tests;

public class MemoryCacheStoreTests
{
    private static MemoryCacheStore CreateStore()
        => new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var store = CreateStore();

        var found = store.TryGet<string>("missing", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsValue()
    {
        var store = CreateStore();

        store.Set("key1", "hello");
        var found = store.TryGet<string>("key1", out var value);

        Assert.True(found);
        Assert.Equal("hello", value);
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
    public async Task TryGetAsync_MissingKey_ReturnsFalse()
    {
        var store = CreateStore();

        var (found, value) = await store.TryGetAsync<string>("missing");

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public async Task SetAsync_ThenTryGetAsync_ReturnsValue()
    {
        var store = CreateStore();

        await store.SetAsync("key1", "async-hello");
        var (found, value) = await store.TryGetAsync<string>("key1");

        Assert.True(found);
        Assert.Equal("async-hello", value);
    }

    [Fact]
    public void Set_WithOptions_StoresValue()
    {
        var store = CreateStore();
        var options = new CleverCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        };

        store.Set("key1", 99, options);

        Assert.True(store.TryGet<int>("key1", out var val));
        Assert.Equal(99, val);
    }

    [Fact]
    public void RegisterEvictionCallback_CalledWhenEntryEvicted()
    {
        var store = CreateStore();
        var tcs = new TaskCompletionSource<object?>();
        store.RegisterEvictionCallback(k => tcs.TrySetResult(k));

        store.Set("key1", 42);
        store.Remove("key1"); // triggers PostEvictionCallback (async)

        Assert.True(tcs.Task.Wait(TimeSpan.FromSeconds(2)), "eviction callback was not fired");
        Assert.Equal("key1", tcs.Task.Result);
    }
}
