using CleverCache.Implementations;
using Microsoft.Extensions.Caching.Memory;

namespace CleverCache.Tests;

public class CacheInvalidationRaceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetOrCreateAsync_InvalidatedDuringFactory_NextReadReturnsFreshValue(bool enableGuard)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateService(new MemoryCacheStore(memory), enableGuard);
        var factoryEntered = NewSignal();
        var releaseFactory = NewSignal();
        var databaseStatus = "AwaitingPayment";
        var cancellationToken = TestContext.Current.CancellationToken;

        var fill = cache.GetOrCreateAsync([typeof(Order)], "order-list", async () =>
        {
            var snapshot = databaseStatus;
            factoryEntered.TrySetResult();
            await releaseFactory.Task.WaitAsync(Timeout, cancellationToken);
            return snapshot;
        }, cancellationToken: cancellationToken);

        try
        {
            await factoryEntered.Task.WaitAsync(Timeout, cancellationToken);
            databaseStatus = "Won";

            // Invalidation must finish without waiting for the blocked query factory.
            await cache.RemoveByTypeAsync(typeof(Order), cancellationToken).WaitAsync(Timeout, cancellationToken);
        }
        finally
        {
            releaseFactory.TrySetResult();
        }

        // The original caller may receive its snapshot, but it must not poison later reads.
        Assert.Equal("AwaitingPayment", await fill.WaitAsync(Timeout, cancellationToken));
        await cache.RemoveByTypeAsync(typeof(Order), cancellationToken);
        var nextRead = await cache.GetOrCreateAsync([typeof(Order)], "order-list",
            () => Task.FromResult(databaseStatus), cancellationToken: cancellationToken);

        Assert.Equal("Won", nextRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetOrCreate_InvalidatedDuringFactory_NextReadReturnsFreshValue(bool enableGuard)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateService(new MemoryCacheStore(memory), enableGuard);
        var factoryEntered = NewSignal();
        var releaseFactory = NewSignal();
        var databaseStatus = "AwaitingPayment";
        var cancellationToken = TestContext.Current.CancellationToken;

        var fill = Task.Factory.StartNew(() => cache.GetOrCreate([typeof(Order)], "order-list", () =>
        {
            var snapshot = databaseStatus;
            factoryEntered.TrySetResult();
            releaseFactory.Task.WaitAsync(Timeout, cancellationToken).GetAwaiter().GetResult();
            return snapshot;
        }), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        try
        {
            await factoryEntered.Task.WaitAsync(Timeout, cancellationToken);
            databaseStatus = "Won";
            await Task.Factory.StartNew(() => cache.RemoveByType(typeof(Order)), cancellationToken,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .WaitAsync(Timeout, cancellationToken);
        }
        finally
        {
            releaseFactory.TrySetResult();
        }

        Assert.Equal("AwaitingPayment", await fill.WaitAsync(Timeout, cancellationToken));
        cache.RemoveByType(typeof(Order));
        var nextRead = cache.GetOrCreate([typeof(Order)], "order-list", () => databaseStatus);

        Assert.Equal("Won", nextRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedEvictionOfOldEntry_ReplacementRemainsClearable(bool enableGuard)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var store = new GatedEvictionStore(memory);
        var cache = CreateService(store, enableGuard);
        var cancellationToken = TestContext.Current.CancellationToken;
        var canonicalKey = CacheKeyIdentity.ToCanonicalKey("order-list");

        cache.GetOrCreate([typeof(Order)], "order-list", () => "AwaitingPayment");
        cache.RemoveByType(typeof(Order));

        try
        {
            await store.CallbackEntered.Task.WaitAsync(Timeout, cancellationToken);

            // Refresh after payment while the old entry's eviction notification is delayed.
            cache.GetOrCreate([typeof(Order)], "order-list", () => "Paid");
            Assert.Contains(canonicalKey, cache.GetDiagnostics().KeysByType[typeof(Order)]);
        }
        finally
        {
            store.ReleaseCallback.TrySetResult();
        }

        await store.CallbackCompleted.Task.WaitAsync(Timeout, cancellationToken);

        // Mimic the controller's clear-all operation, which enumerates tracked keys.
        foreach (var key in cache.GetDiagnostics().KeysByType.Values.SelectMany(keys => keys).Distinct())
            cache.Remove(key);

        // A subsequent won update must also be able to invalidate the replacement.
        cache.RemoveByType(typeof(Order));
        var nextRead = cache.GetOrCreate([typeof(Order)], "order-list", () => "Won");

        Assert.Equal("Won", nextRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoveDuringFactory_NextReadReturnsFreshValue(bool enableGuard)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateService(new MemoryCacheStore(memory), enableGuard);
        var entered = NewSignal();
        var release = NewSignal();
        var cancellationToken = TestContext.Current.CancellationToken;
        var fill = cache.GetOrCreateAsync([typeof(Order)], "order", async () =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(Timeout, cancellationToken);
            return "AwaitingPayment";
        }, cancellationToken: cancellationToken);

        try
        {
            await entered.Task.WaitAsync(Timeout, cancellationToken);
            cache.Remove("order");
        }
        finally
        {
            release.TrySetResult();
        }

        await fill.WaitAsync(Timeout, cancellationToken);
        Assert.Equal("Won", cache.GetOrCreate([typeof(Order)], "order", () => "Won"));
    }

    [Fact]
    public async Task InvalidationDuringAsyncStoreWrite_NextReadReturnsFreshValue()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var store = new GatedWriteStore(memory);
        var cache = CreateService(store, false);
        var cancellationToken = TestContext.Current.CancellationToken;
        var fill = cache.GetOrCreateAsync([typeof(Order)], "order",
            () => Task.FromResult("AwaitingPayment"), cancellationToken: cancellationToken);
        await store.WriteEntered.Task.WaitAsync(Timeout, cancellationToken);
        var invalidate = cache.RemoveByTypeAsync(typeof(Order), cancellationToken);
        store.ReleaseWrite.TrySetResult();

        await Task.WhenAll(fill, invalidate).WaitAsync(Timeout, cancellationToken);

        Assert.Equal("Won", cache.GetOrCreate([typeof(Order)], "order", () => "Won"));
    }

    [Fact]
    public async Task FailedFactory_DoesNotLeaveTrackedKey()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateService(new MemoryCacheStore(memory), false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetOrCreateAsync<string>([typeof(Order)], "order",
                () => throw new InvalidOperationException("Query failed"),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(cache.GetDiagnostics().KeysByType.Values.SelectMany(keys => keys));
    }

    [Fact]
    public async Task CancellationAfterFactory_DoesNotLeaveTrackedKey()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateService(new MemoryCacheStore(memory), false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.GetOrCreateAsync([typeof(Order)], "order", () =>
            {
                cancellation.Cancel();
                return Task.FromResult("Paid");
            }, cancellationToken: cancellation.Token));

        Assert.Empty(cache.GetDiagnostics().KeysByType.Values.SelectMany(keys => keys));
        Assert.Equal("Won", cache.GetOrCreate([typeof(Order)], "order", () => "Won"));
    }

    [Fact]
    public async Task DelayedEvictionDuringReplacementFactory_ReplacementRemainsTracked()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var store = new GatedEvictionStore(memory);
        var cache = CreateService(store, false);
        var entered = NewSignal();
        var release = NewSignal();
        var cancellationToken = TestContext.Current.CancellationToken;
        cache.GetOrCreate([typeof(Order)], "order", () => "AwaitingPayment");
        cache.Remove("order");
        await store.CallbackEntered.Task.WaitAsync(Timeout, cancellationToken);

        var replacement = cache.GetOrCreateAsync([typeof(Order)], "order", async () =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(Timeout, cancellationToken);
            return "Paid";
        }, cancellationToken: cancellationToken);

        try
        {
            await entered.Task.WaitAsync(Timeout, cancellationToken);
            store.ReleaseCallback.TrySetResult();
            await store.CallbackCompleted.Task.WaitAsync(Timeout, cancellationToken);
        }
        finally
        {
            store.ReleaseCallback.TrySetResult();
            release.TrySetResult();
        }

        await replacement.WaitAsync(Timeout, cancellationToken);
        Assert.Contains(CacheKeyIdentity.ToCanonicalKey("order"), cache.GetDiagnostics().KeysByType[typeof(Order)]);
        cache.RemoveByType(typeof(Order));
        Assert.Equal("Won", cache.GetOrCreate([typeof(Order)], "order", () => "Won"));
    }

    [Fact]
    public async Task GenuineStoreEviction_CleansUpTracking()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var store = new GatedEvictionStore(memory);
        var cache = CreateService(store, false);
        cache.GetOrCreate([typeof(Order)], "order", () => "Paid");

        // Evict directly from the store, rather than via CleverCache's explicit removal.
        store.Remove(CacheKeyIdentity.ToCanonicalKey("order"));
        store.ReleaseCallback.TrySetResult();
        await store.CallbackCompleted.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Empty(cache.GetDiagnostics().KeysByType.Values.SelectMany(keys => keys));
    }

    [Fact]
    public async Task InvalidationOfDependentTypeDuringFactory_DoesNotCacheSnapshot()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateService(new MemoryCacheStore(memory), false);
        cache.AddDependentCache(typeof(Order), typeof(Transaction));
        cache.AddDependentCache(typeof(Transaction), typeof(Customer));
        var entered = NewSignal();
        var release = NewSignal();
        var cancellationToken = TestContext.Current.CancellationToken;
        var fill = cache.GetOrCreateAsync([typeof(Order)], "order", async () =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(Timeout, cancellationToken);
            return "AwaitingPayment";
        }, cancellationToken: cancellationToken);

        try
        {
            await entered.Task.WaitAsync(Timeout, cancellationToken);
            await cache.RemoveByTypeAsync(typeof(Customer), cancellationToken);
        }
        finally
        {
            release.TrySetResult();
        }

        await fill.WaitAsync(Timeout, cancellationToken);
        Assert.Equal("Won", cache.GetOrCreate([typeof(Order)], "order", () => "Won"));
    }

    [Fact]
    public async Task ConcurrentFactories_NewGenerationSurvivesOlderFactoryCompletion()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateService(new MemoryCacheStore(memory), false);
        var entered = NewSignal();
        var release = NewSignal();
        var cancellationToken = TestContext.Current.CancellationToken;
        var oldFill = cache.GetOrCreateAsync([typeof(Order)], "order", async () =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(Timeout, cancellationToken);
            return "AwaitingPayment";
        }, cancellationToken: cancellationToken);

        try
        {
            await entered.Task.WaitAsync(Timeout, cancellationToken);
            cache.RemoveByType(typeof(Order));
            Assert.Equal("Paid", cache.GetOrCreate([typeof(Order)], "order", () => "Paid"));
        }
        finally
        {
            release.TrySetResult();
        }

        await oldFill.WaitAsync(Timeout, cancellationToken);
        Assert.Equal("Paid", cache.GetOrCreate([typeof(Order)], "order", () => "Wrong"));
        cache.RemoveByType(typeof(Order));
        Assert.Equal("Won", cache.GetOrCreate([typeof(Order)], "order", () => "Won"));
    }

    [Fact]
    public async Task BlockedStoreWrite_DoesNotBlockUnrelatedKeys()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var store = new GatedWriteStore(memory);
        var cache = CreateService(store, false);
        var cancellationToken = TestContext.Current.CancellationToken;
        var fill = cache.GetOrCreateAsync([typeof(Order)], "order",
            () => Task.FromResult("Paid"), cancellationToken: cancellationToken);
        try
        {
            await store.WriteEntered.Task.WaitAsync(Timeout, cancellationToken);
            await Task.Run(() =>
            {
                Assert.Equal("Fresh", cache.GetOrCreate([typeof(Customer)], "customer", () => "Fresh"));
                cache.RemoveByType(typeof(Customer));
            }, cancellationToken).WaitAsync(Timeout, cancellationToken);
        }
        finally
        {
            store.ReleaseWrite.TrySetResult();
        }

        await fill.WaitAsync(Timeout, cancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoreWritesThenThrows_EntryRemainsClearable(bool asyncWrite)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var store = new ThrowingWriteStore(memory);
        var cache = CreateService(store, false);

        if (asyncWrite)
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                cache.GetOrCreateAsync([typeof(Order)], "order", () => Task.FromResult("Paid"),
                    cancellationToken: TestContext.Current.CancellationToken));
        else
            Assert.Throws<InvalidOperationException>(() =>
                cache.GetOrCreate([typeof(Order)], "order", () => "Paid"));

        Assert.Contains(CacheKeyIdentity.ToCanonicalKey("order"), cache.GetDiagnostics().KeysByType[typeof(Order)]);
        cache.RemoveByType(typeof(Order));
        Assert.Empty(cache.GetDiagnostics().KeysByType.Values.SelectMany(keys => keys));
    }

    [Fact]
    public async Task SynchronousEvictionCallbacks_DoNotDeadlockOrUntrackReplacement()
    {
        await Task.Run(() =>
        {
            var store = new InlineEvictionStore();
            var cache = CreateService(store, false);
            cache.GetOrCreate([typeof(Order)], "order", () => "Paid");
            store.NotifyEviction(CacheKeyIdentity.ToCanonicalKey("order"));
            Assert.Contains(CacheKeyIdentity.ToCanonicalKey("order"), cache.GetDiagnostics().KeysByType[typeof(Order)]);
            cache.RemoveByType(typeof(Order));
            Assert.Equal("Won", cache.GetOrCreate([typeof(Order)], "order", () => "Won"));
        }, TestContext.Current.CancellationToken).WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void EvictionNotification_CachedNullRemainsTrackedUnderAllTypes()
    {
        var store = new InlineEvictionStore();
        var cache = CreateService(store, false);
        Type[] types = [typeof(Order), typeof(Customer)];
        cache.GetOrCreate<string?>(types, "order", () => null);
        store.NotifyEviction(CacheKeyIdentity.ToCanonicalKey("order"));

        Assert.Null(cache.GetOrCreate<string?>(types, "order", () => throw new InvalidOperationException("Expected a cache hit")));
        Assert.Contains(CacheKeyIdentity.ToCanonicalKey("order"), cache.GetDiagnostics().KeysByType[typeof(Order)]);
        Assert.Contains(CacheKeyIdentity.ToCanonicalKey("order"), cache.GetDiagnostics().KeysByType[typeof(Customer)]);
        cache.RemoveByType(typeof(Customer));
        Assert.Equal("Won", cache.GetOrCreate(types, "order", () => "Won"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedConcurrentReplacementAndInvalidation_FinalEntryRemainsClearable(bool enableGuard)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateService(new MemoryCacheStore(memory), enableGuard);
        var cancellationToken = TestContext.Current.CancellationToken;

        for (var round = 0; round < 100; round++)
        {
            await Task.Run(() => Parallel.For(0, 20,
                new ParallelOptions { CancellationToken = cancellationToken }, i =>
                {
                    if (i % 3 == 0) cache.RemoveByType(typeof(Order));
                    cache.GetOrCreate([typeof(Order)], "order", () => "Old");
                }), cancellationToken).WaitAsync(Timeout, cancellationToken);

            cache.RemoveByType(typeof(Order));
            Assert.Equal("Won", cache.GetOrCreate([typeof(Order)], "order", () => "Won"));
            Assert.Contains(CacheKeyIdentity.ToCanonicalKey("order"), cache.GetDiagnostics().KeysByType[typeof(Order)]);
            cache.Remove("order");
            Assert.Empty(cache.GetDiagnostics().KeysByType.Values.SelectMany(keys => keys));
        }
    }

    private sealed class GatedWriteStore(IMemoryCache memory) : ICleverCacheStore
    {
        private readonly MemoryCacheStore _inner = new(memory);
        public TaskCompletionSource WriteEntered { get; } = NewSignal();
        public TaskCompletionSource ReleaseWrite { get; } = NewSignal();

        public bool TryGet<TItem>(object key, out TItem? value) => _inner.TryGet(key, out value);
        public Task<(bool Hit, TItem? Value)> TryGetAsync<TItem>(object key, CancellationToken cancellationToken = default) =>
            _inner.TryGetAsync<TItem>(key, cancellationToken);
        public void Set<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null) =>
            _inner.Set(key, value, options);
        public async Task SetAsync<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
        {
            WriteEntered.TrySetResult();
            await ReleaseWrite.Task.WaitAsync(Timeout, cancellationToken);
            _inner.Set(key, value, options);
        }
        public void Remove(object key) => _inner.Remove(key);
        public Task RemoveAsync(object key, CancellationToken cancellationToken = default) =>
            _inner.RemoveAsync(key, cancellationToken);
    }

    private static CleverCacheService CreateService(ICleverCacheStore store, bool enableGuard) =>
        new(store, new CleverCacheOptions { EnableAsyncRaceConditionGuard = enableGuard });

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Order;
    private sealed class Transaction;
    private sealed class Customer;

    private sealed class ThrowingWriteStore(IMemoryCache memory) : ICleverCacheStore
    {
        private readonly MemoryCacheStore _inner = new(memory);
        public bool TryGet<TItem>(object key, out TItem? value) => _inner.TryGet(key, out value);
        public Task<(bool Hit, TItem? Value)> TryGetAsync<TItem>(object key, CancellationToken cancellationToken = default) =>
            _inner.TryGetAsync<TItem>(key, cancellationToken);
        public void Set<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null)
        {
            _inner.Set(key, value, options);
            throw new InvalidOperationException("Store wrote before reporting failure");
        }
        public Task SetAsync<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
        public void Remove(object key) => _inner.Remove(key);
        public Task RemoveAsync(object key, CancellationToken cancellationToken = default) =>
            _inner.RemoveAsync(key, cancellationToken);
    }

    private sealed class InlineEvictionStore : ICleverCacheStore, IEvictionNotifyingStore
    {
        private readonly Dictionary<object, object?> _values = new();
        private Action<object>? _callback;
        public void RegisterEvictionCallback(Action<object> onEvicted) => _callback = onEvicted;
        public void NotifyEviction(object key) => _callback?.Invoke(key);
        public bool TryGet<TItem>(object key, out TItem? value)
        {
            var hit = _values.TryGetValue(key, out var stored);
            value = hit ? (TItem?)stored : default;
            return hit;
        }
        public Task<(bool Hit, TItem? Value)> TryGetAsync<TItem>(object key, CancellationToken cancellationToken = default)
        {
            var hit = TryGet<TItem>(key, out var value);
            return Task.FromResult((hit, value));
        }
        public void Set<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null) => _values[key] = value;
        public Task SetAsync<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
        public void Remove(object key)
        {
            _values.Remove(key);
            NotifyEviction(key);
        }
        public Task RemoveAsync(object key, CancellationToken cancellationToken = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class GatedEvictionStore(IMemoryCache memory) : ICleverCacheStore, IEvictionNotifyingStore
    {
        private readonly MemoryCacheStore _inner = new(memory);
        private int _callbackCount;

        public TaskCompletionSource CallbackEntered { get; } = NewSignal();
        public TaskCompletionSource ReleaseCallback { get; } = NewSignal();
        public TaskCompletionSource CallbackCompleted { get; } = NewSignal();

        public void RegisterEvictionCallback(Action<object> onEvicted) =>
            _inner.RegisterEvictionCallback(key =>
            {
                if (Interlocked.Increment(ref _callbackCount) != 1)
                {
                    onEvicted(key);
                    return;
                }

                CallbackEntered.TrySetResult();
                try
                {
                    ReleaseCallback.Task.WaitAsync(Timeout).GetAwaiter().GetResult();
                    onEvicted(key);
                    CallbackCompleted.TrySetResult();
                }
                catch (Exception exception)
                {
                    CallbackCompleted.TrySetException(exception);
                }
            });

        public bool TryGet<TItem>(object key, out TItem? value) => _inner.TryGet(key, out value);

        public Task<(bool Hit, TItem? Value)> TryGetAsync<TItem>(object key, CancellationToken cancellationToken = default) =>
            _inner.TryGetAsync<TItem>(key, cancellationToken);

        public void Set<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null) =>
            _inner.Set(key, value, options);

        public Task SetAsync<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default) =>
            _inner.SetAsync(key, value, options, cancellationToken);

        public void Remove(object key) => _inner.Remove(key);

        public Task RemoveAsync(object key, CancellationToken cancellationToken = default) =>
            _inner.RemoveAsync(key, cancellationToken);
    }
}
