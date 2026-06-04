using CleverCache.Implementations;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace CleverCache.Tests;

public class CleverCacheServiceTests
{
    private static CleverCacheService CreateService(ICleverCacheStore? store = null, CleverCacheOptions? options = null)
        => new(
            store ?? new MemoryCacheStore(new MemoryCache(new MemoryCacheOptions())),
            options ?? new CleverCacheOptions());

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
    public void GetOrCreate_EquivalentObjectKeys_ReuseSameEntry()
    {
        var sut = CreateService();
        var callCount = 0;

        var key1 = new QueryKey(123, ["one", "two"]);
        var key2 = new QueryKey(123, ["one", "two"]);

        var first = sut.GetOrCreate([typeof(string)], key1, () => { callCount++; return 42; });
        var second = sut.GetOrCreate([typeof(string)], key2, () => { callCount++; return 99; });

        Assert.Equal(42, first);
        Assert.Equal(42, second);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void GetOrCreate_DifferentKeyTypesSameData_DoNotCollide()
    {
        var sut = CreateService();
        var callCount = 0;

        var a = sut.GetOrCreate([typeof(string)], new QueryKeyA(1), () => { callCount++; return "A"; });
        var b = sut.GetOrCreate([typeof(string)], new QueryKeyB(1), () => { callCount++; return "B"; });

        Assert.Equal("A", a);
        Assert.Equal("B", b);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetOrCreate_ConcurrentRequestsSameKey_WithRaceConditionGuardEnabled_FactoryCalledOnce()
    {
        var sut = CreateService(options: new CleverCacheOptions { EnableAsyncRaceConditionGuard = true });
        var callCount = 0;
        using var gate = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
            sut.GetOrCreate([typeof(string)], "sync-stampede-key", () =>
            {
                gate.Wait(TestContext.Current.CancellationToken);
                Interlocked.Increment(ref callCount);
                return 42;
            }), TestContext.Current.CancellationToken)).ToArray();

        await Task.Delay(30, TestContext.Current.CancellationToken);
        gate.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, callCount);
        Assert.All(results, r => Assert.Equal(42, r));
    }

    [Fact]
    public async Task GetOrCreate_ConcurrentRequestsSameKey_WithRaceConditionGuardDisabled_AllowsConcurrentFactoryExecution()
    {
        const int threadCount = 20;
        var sut = CreateService();
        var callCount = 0;
        // Gate blocks factory from completing until all threads are inside,
        // guaranteeing concurrent execution regardless of thread pool scheduling.
        using var gate = new ManualResetEventSlim(false);
        using var allInFactory = new CountdownEvent(threadCount);

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
            sut.GetOrCreate([typeof(string)], "sync-stampede-key-disabled-guard", () =>
            {
                Interlocked.Increment(ref callCount);
                allInFactory.Signal();
                gate.Wait(TestContext.Current.CancellationToken);
                return 42;
            }), TestContext.Current.CancellationToken)).ToArray();

        // Wait until all threads are inside the factory, then release them
        allInFactory.Wait(TestContext.Current.CancellationToken);
        gate.Set();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(threadCount, callCount);
        Assert.All(results, r => Assert.Equal(42, r));
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheMiss_InvokesFactory()
    {
        var sut = CreateService();
        var callCount = 0;

        var result = await sut.GetOrCreateAsync([typeof(string)], "key1",
            async () => { callCount++; await Task.Yield(); return 42; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(42, result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheHit_DoesNotInvokeFactoryAgain()
    {
        var sut = CreateService();
        var callCount = 0;

        await sut.GetOrCreateAsync([typeof(string)], "key1",
            async () => { callCount++; await Task.Yield(); return 42; },
            cancellationToken: TestContext.Current.CancellationToken);
        var result = await sut.GetOrCreateAsync([typeof(string)], "key1",
            async () => { callCount++; await Task.Yield(); return 99; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(42, result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Remove_AllowsFactoryToBeCalledAgain()
    {
        var sut = CreateService();
        var callCount = 0;

        await sut.GetOrCreateAsync([typeof(string)], "key1", async () => { callCount++; await Task.Yield(); return 1; }, cancellationToken: TestContext.Current.CancellationToken);
        sut.Remove("key1");
        await sut.GetOrCreateAsync([typeof(string)], "key1", async () => { callCount++; await Task.Yield(); return 2; }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Remove_EquivalentObjectKey_RemovesMatchingEntry()
    {
        var sut = CreateService();
        var callCount = 0;

        var key1 = new QueryKey(321, ["x", "y"]);
        var key2 = new QueryKey(321, ["x", "y"]);

        sut.GetOrCreate([typeof(string)], key1, () => { callCount++; return 1; });
        sut.Remove(key2);
        sut.GetOrCreate([typeof(string)], key1, () => { callCount++; return 2; });

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task RemoveByType_RemovesAllKeysForType()
    {
        var sut = CreateService();
        var callCount = 0;

        await sut.GetOrCreateAsync([typeof(string)], "key1", async () => { await Task.Yield(); return 1; }, cancellationToken: TestContext.Current.CancellationToken);
        await sut.GetOrCreateAsync([typeof(string)], "key2", async () => { await Task.Yield(); return 2; }, cancellationToken: TestContext.Current.CancellationToken);

        sut.RemoveByType(typeof(string));

        await sut.GetOrCreateAsync([typeof(string)], "key1", async () => { callCount++; await Task.Yield(); return 99; }, cancellationToken: TestContext.Current.CancellationToken);
        await sut.GetOrCreateAsync([typeof(string)], "key2", async () => { callCount++; await Task.Yield(); return 99; }, cancellationToken: TestContext.Current.CancellationToken);

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
                async () => { await Task.Yield(); return 99; },
                cancellationToken: TestContext.Current.CancellationToken);

            outerCompleted = true;
            return $"result:{inner}";
        }, cancellationToken: TestContext.Current.CancellationToken);

        // If deadlocked, this will throw after the timeout rather than hang the test suite
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.Same(task, completed); // task finished, not the timeout
        Assert.True(outerCompleted);
        Assert.Equal("result:99", await task);
    }

    [Fact]
    public async Task GetOrCreateAsync_ConcurrentRequestsSameKey_FactoryCalledOnce()
    {
        var sut = CreateService(options: new CleverCacheOptions { EnableAsyncRaceConditionGuard = true });
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
    public async Task GetOrCreateAsync_ConcurrentRequestsSameKey_WithRaceConditionGuardDisabled_AllowsConcurrentFactoryExecution()
    {
        var sut = CreateService();
        var callCount = 0;
        var inFlight = 0;
        var maxInFlight = 0;

        var tasks = Enumerable.Range(0, 20).Select(_ =>
            sut.GetOrCreateAsync([typeof(string)], "stampede-key-disabled-guard", async () =>
            {
                Interlocked.Increment(ref callCount);
                var current = Interlocked.Increment(ref inFlight);
                while (true)
                {
                    var observedMax = maxInFlight;
                    if (current <= observedMax || Interlocked.CompareExchange(ref maxInFlight, current, observedMax) == observedMax)
                        break;
                }

                await Task.Delay(30, TestContext.Current.CancellationToken);
                Interlocked.Decrement(ref inFlight);
                return 42;
            })).ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.True(callCount > 1);
        Assert.True(maxInFlight > 1);
        Assert.All(results, r => Assert.Equal(42, r));
    }

    [Fact]
    public void GetDiagnostics_ReturnsCascadesAndKeys()
    {
        var sut = CreateService();
        sut.AddDependentCache(typeof(string), typeof(int));
        sut.GetOrCreate([typeof(string)], "k1", () => 1);

        var d = sut.GetDiagnostics();
        var expected = CacheKeyIdentity.ToCanonicalKey("k1");

        Assert.Contains(typeof(int), d.Dependants[typeof(string)]);
        Assert.Contains(expected, d.KeysByType[typeof(string)]);
    }

    [Fact]
    public void GetDiagnostics_ComplexKey_IsProjectedToSerializableValue()
    {
        var sut = CreateService();
        Func<int> complexKey = () => 123;

        sut.GetOrCreate([typeof(string)], complexKey, () => 1);

        var d = sut.GetDiagnostics();
        var key = Assert.Single(d.KeysByType[typeof(string)]);

        var keyText = Assert.IsType<string>(key);
        Assert.False(string.IsNullOrWhiteSpace(keyText));
    }

    [Fact]
    public void GetDiagnostics_KeyWithDeferredEnumerable_UsesMaterializedValues()
    {
        var sut = CreateService();
        var key = new DeferredNamesQuery(new[] { "Size", "Color" }.Select(static x => x));

        sut.GetOrCreate([typeof(string)], key, () => 1);

        var d = sut.GetDiagnostics();
        var keyText = Assert.IsType<string>(Assert.Single(d.KeysByType[typeof(string)]));

        Assert.Contains("DeferredNamesQuery", keyText);
        Assert.Contains("\"names\":[\"Size\",\"Color\"]", keyText);
        Assert.DoesNotContain("ListSelectIterator", keyText);
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
        await sut.RemoveByTypeAsync(typeof(string), TestContext.Current.CancellationToken);

        storeMock.Verify(s => s.RemoveAsync(CacheKeyIdentity.ToCanonicalKey("k1"), It.IsAny<CancellationToken>()), Times.Once);
        storeMock.Verify(s => s.RemoveAsync(CacheKeyIdentity.ToCanonicalKey("k2"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Remove_CleansUpKeyFromAllTypeSets()
    {
        var sut = CreateService();
        sut.AddDependentCache(typeof(string), typeof(int));
        sut.GetOrCreate([typeof(string)], "k1", () => 1); // k1 added to String and Int32

        sut.Remove("k1");

        var d = sut.GetDiagnostics();
        Assert.DoesNotContain("k1", d.KeysByType.GetValueOrDefault(typeof(string)) ?? []);
        Assert.DoesNotContain("k1", d.KeysByType.GetValueOrDefault(typeof(int)) ?? []);
    }

    [Fact]
    public void RemoveByType_CleansUpKeysFromAllTypeSets()
    {
        var sut = CreateService();
        sut.AddDependentCache(typeof(string), typeof(int));
        sut.GetOrCreate([typeof(string)], "k1", () => 1); // k1 added to String and Int32 transitively

        sut.RemoveByType(typeof(string));

        var d = sut.GetDiagnostics();
        Assert.DoesNotContain("k1", d.KeysByType.GetValueOrDefault(typeof(string)) ?? []);
        Assert.DoesNotContain("k1", d.KeysByType.GetValueOrDefault(typeof(int)) ?? []); // must also clean up dependent type
    }

    private sealed record DeferredNamesQuery(IEnumerable<string> Names);
    private sealed class QueryKey
    {
        public QueryKey(int id, IEnumerable<string> names)
        {
            Id = id;
            Names = names;
        }

        public int Id { get; }
        public IEnumerable<string> Names { get; }
    }

    private sealed class QueryKeyA
    {
        public QueryKeyA(int id) => Id = id;
        public int Id { get; }
    }

    private sealed class QueryKeyB
    {
        public QueryKeyB(int id) => Id = id;
        public int Id { get; }
    }

    [Fact]
    public void Eviction_WhenStoreSupportsNotification_CleansUpKeyFromTypeSets()
    {
        // MemoryCacheStore implements IEvictionNotifyingStore — eviction should clean up _keysByType
        var sut = CreateService();
        sut.GetOrCreate([typeof(string)], "k1", () => 1);
        var expected = CacheKeyIdentity.ToCanonicalKey("k1");

        Assert.Contains(expected, sut.GetDiagnostics().KeysByType[typeof(string)]);

        sut.Remove("k1"); // triggers store eviction → PostEvictionCallback → RemoveKeyFromAllTypes

        Assert.DoesNotContain(expected, sut.GetDiagnostics().KeysByType.GetValueOrDefault(typeof(string)) ?? []);
    }
}
