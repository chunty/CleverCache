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

    [Fact]
    public void Set_EquivalentObjectKeys_ReusesSameEntry()
    {
        var store = CreateStore();

        store.Set(new DistQueryKey(7, ["one", "two"]), "value-a");
        var found = store.TryGet<string>(new DistQueryKey(7, ["one", "two"]), out var value);

        Assert.True(found);
        Assert.Equal("value-a", value);
    }

    [Fact]
    public void Set_DifferentTypesSameData_DoNotCollide()
    {
        var store = CreateStore();

        store.Set(new DistQueryKeyA(9), "value-a");
        store.Set(new DistQueryKeyB(9), "value-b");

        Assert.True(store.TryGet<string>(new DistQueryKeyA(9), out var valueA));
        Assert.True(store.TryGet<string>(new DistQueryKeyB(9), out var valueB));
        Assert.Equal("value-a", valueA);
        Assert.Equal("value-b", valueB);
    }

    private sealed class DistQueryKey
    {
        public DistQueryKey(int id, IEnumerable<string> names)
        {
            Id = id;
            Names = names;
        }

        public int Id { get; }
        public IEnumerable<string> Names { get; }
    }

    private sealed class DistQueryKeyA
    {
        public DistQueryKeyA(int id) => Id = id;
        public int Id { get; }
    }

    private sealed class DistQueryKeyB
    {
        public DistQueryKeyB(int id) => Id = id;
        public int Id { get; }
    }
}
