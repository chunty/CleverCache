using CleverCache.Implementations;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace CleverCache.Tests;

public class CleverCacheServiceTests
{
    private static CleverCacheService CreateService(ICleverCacheStore? store = null)
        => new(store ?? new MemoryCacheStore(new MemoryCache(new MemoryCacheOptions())), new CleverCacheOptions());

    [Fact]
    public void GetOrCreate_CacheMiss_InvokesFactory()
    {
        var sut = CreateService();
        var callCount = 0;

        var result = sut.GetOrCreate([typeof(string)], "key1", () => { callCount++; return 42; });

        Assert.Equal(42, result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void GetOrCreate_CacheHit_DoesNotInvokeFactoryAgain()
    {
        var sut = CreateService();
        var callCount = 0;

        sut.GetOrCreate([typeof(string)], "key1", () => { callCount++; return 42; });
        var result = sut.GetOrCreate([typeof(string)], "key1", () => { callCount++; return 99; });

        Assert.Equal(42, result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheMiss_InvokesFactory()
    {
        var sut = CreateService();
        var callCount = 0;

        var result = await sut.GetOrCreateAsync([typeof(string)], "key1",
            async () => { callCount++; await Task.Yield(); return 42; });

        Assert.Equal(42, result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheHit_DoesNotInvokeFactoryAgain()
    {
        var sut = CreateService();
        var callCount = 0;

        await sut.GetOrCreateAsync([typeof(string)], "key1",
            async () => { callCount++; await Task.Yield(); return 42; });
        var result = await sut.GetOrCreateAsync([typeof(string)], "key1",
            async () => { callCount++; await Task.Yield(); return 99; });

        Assert.Equal(42, result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Remove_AllowsFactoryToBeCalledAgain()
    {
        var sut = CreateService();
        var callCount = 0;

        await sut.GetOrCreateAsync([typeof(string)], "key1", async () => { callCount++; await Task.Yield(); return 1; });
        sut.Remove("key1");
        await sut.GetOrCreateAsync([typeof(string)], "key1", async () => { callCount++; await Task.Yield(); return 2; });

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task RemoveByType_RemovesAllKeysForType()
    {
        var sut = CreateService();
        var callCount = 0;

        await sut.GetOrCreateAsync([typeof(string)], "key1", async () => { await Task.Yield(); return 1; });
        await sut.GetOrCreateAsync([typeof(string)], "key2", async () => { await Task.Yield(); return 2; });

        sut.RemoveByType(typeof(string));

        await sut.GetOrCreateAsync([typeof(string)], "key1", async () => { callCount++; await Task.Yield(); return 99; });
        await sut.GetOrCreateAsync([typeof(string)], "key2", async () => { callCount++; await Task.Yield(); return 99; });

        Assert.Equal(2, callCount);
    }

    /// <summary>
    /// Regression test: with the old global SemaphoreSlim, a factory that triggered another
    /// cached call (different key) would deadlock indefinitely. Per-key locking must not deadlock.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_NestedCachedCallWithDifferentKey_DoesNotDeadlock()
    {
        var sut = CreateService();

        var outerCompleted = false;

        var task = sut.GetOrCreateAsync([typeof(string)], "outer-key", async () =>
        {
            // Simulate a nested cached call with a DIFFERENT key — the original deadlock scenario
            var inner = await sut.GetOrCreateAsync([typeof(int)], "inner-key",
                async () => { await Task.Yield(); return 99; });

            outerCompleted = true;
            return $"result:{inner}";
        });

        // If deadlocked, this will throw after the timeout rather than hang the test suite
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(task, completed); // task finished, not the timeout
        Assert.True(outerCompleted);
        Assert.Equal("result:99", await task);
    }

    [Fact]
    public async Task GetOrCreateAsync_ConcurrentRequestsSameKey_FactoryCalledOnce()
    {
        var sut = CreateService();
        var callCount = 0;
        var tcs = new TaskCompletionSource();

        // 20 concurrent calls all waiting on the same factory
        var tasks = Enumerable.Range(0, 20).Select(_ =>
            sut.GetOrCreateAsync([typeof(string)], "stampede-key", async () =>
            {
                await tcs.Task; // all wait here until released
                Interlocked.Increment(ref callCount);
                return 42;
            })).ToArray();

        tcs.SetResult(); // release all waiters simultaneously
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, callCount);
        Assert.All(results, r => Assert.Equal(42, r));
    }

    [Fact]
    public void GetDiagnostics_ReturnsCascadesAndKeys()
    {
        var sut = CreateService();
        sut.AddDependentCache(typeof(string), typeof(int));
        sut.GetOrCreate([typeof(string)], "k1", () => 1);

        var d = sut.GetDiagnostics();

        Assert.Contains(typeof(int), d.Dependants[typeof(string)]);
        Assert.Contains("k1", d.KeysByType[typeof(string)]);
    }

    [Fact]
    public void RenderDependencyTree_ContainsTypeNamesAndKeys()
    {
        var sut = CreateService();
        sut.AddDependentCache(typeof(string), typeof(int));
        sut.GetOrCreate([typeof(string)], "my-key", () => 1);

        var output = sut.RenderDependencyTree();

        Assert.Contains("String", output);
        Assert.Contains("Int32", output);
        Assert.Contains("my-key", output);
        Assert.Contains("cascades to", output);
    }

    [Fact]
    public void RenderDependencyTree_NoTypes_ReturnsEmptyMessage()
    {
        var sut = CreateService();

        var output = sut.RenderDependencyTree();

        Assert.Contains("no types registered", output);
    }

    [Fact]
    public async Task RemoveByTypeAsync_RemovesAllKeysForType()
    {
        var storeMock = new Mock<ICleverCacheStore>();
        storeMock.Setup(s => s.TryGet<int>(It.IsAny<object>(), out It.Ref<int>.IsAny)).Returns(false);
        storeMock.Setup(s => s.TryGetAsync<int>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, default(int)));
        storeMock.Setup(s => s.RemoveAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sut = new CleverCacheService(storeMock.Object, new CleverCacheOptions());

        sut.GetOrCreate([typeof(string)], "k1", () => 1);
        sut.GetOrCreate([typeof(string)], "k2", () => 2);
        await sut.RemoveByTypeAsync(typeof(string));

        storeMock.Verify(s => s.RemoveAsync("k1", It.IsAny<CancellationToken>()), Times.Once);
        storeMock.Verify(s => s.RemoveAsync("k2", It.IsAny<CancellationToken>()), Times.Once);
    }
}
