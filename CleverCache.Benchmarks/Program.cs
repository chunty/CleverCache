using System.Diagnostics;
using System.Text.Json;
using System.Collections.Concurrent;
using CleverCache;
using CleverCache.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var measurements = new List<Measurement>();
foreach (var guard in new[] { false, true })
{
    using var provider = new ServiceCollection()
        .AddCleverCache(options => options.EnableAsyncRaceConditionGuard = guard)
        .BuildServiceProvider();
    var cache = provider.GetRequiredService<ICleverCache>();
    Type[] types = [typeof(Order)];
    object stringKey = "order";
    object recordKey = new OrderQuery(42);
    Func<int> factory = () => 42;
    Func<Task<int>> asyncFactory = () => Task.FromResult(42);

    foreach (var (shape, key) in new[] { ("string", stringKey), ("record", recordKey) })
    {
        cache.GetOrCreate(types, key, factory);
        Measure($"sync-hit-{shape}", guard, 100_000, () => cache.GetOrCreate(types, key, factory));
        Measure($"async-hit-{shape}", guard, 100_000,
            () => cache.GetOrCreateAsync(types, key, asyncFactory).GetAwaiter().GetResult());
    }

    Measure("sync-miss-remove", guard, 5_000, () =>
    {
        cache.Remove(stringKey);
        cache.GetOrCreate(types, stringKey, factory);
    });
    Measure("async-miss-remove", guard, 5_000, () =>
    {
        cache.Remove(stringKey);
        cache.GetOrCreateAsync(types, stringKey, asyncFactory).GetAwaiter().GetResult();
    });
    Measure("type-invalidate-refill-32", guard, 100, () =>
    {
        cache.RemoveByType(typeof(Order));
        for (var i = 0; i < 32; i++)
            cache.GetOrCreate(types, $"list-{i}", factory);
    });
    Measure("parallel-same-key-hit", guard, 10, () =>
        Parallel.For(0, 10_000, _ => cache.GetOrCreate(types, stringKey, factory)), 10_000);
    Measure("parallel-unrelated-key-hit", guard, 10, () =>
        Parallel.For(0, 10_000, i => cache.GetOrCreate(types, $"list-{i % 32}", factory)), 10_000);
    Measure("parallel-mixed", guard, 10, () =>
        Parallel.For(0, 1_000, i =>
        {
            var key = $"mixed-{i % 32}";
            if (i % 20 == 0) cache.Remove(key);
            cache.GetOrCreate(types, key, factory);
        }), 1_000);

    var batch = 0;
    Measure("parallel-cold-same-key", guard, 10, () =>
    {
        var key = $"cold-{batch++}";
        Parallel.For(0, 256, _ => cache.GetOrCreate(types, key, () =>
        {
            Thread.SpinWait(1_000);
            return 42;
        }));
    }, 256);
    Measure("parallel-cold-unrelated-keys", guard, 10, () =>
    {
        var prefix = $"cold-many-{batch++}";
        Parallel.For(0, 256, i => cache.GetOrCreate(types, $"{prefix}-{i}", factory));
    }, 256);

    using var dictionaryProvider = new ServiceCollection()
        .AddCleverCache(options =>
        {
            options.EnableAsyncRaceConditionGuard = guard;
            options.UseCustomStore<DictionaryStore>();
        }).BuildServiceProvider();
    var dictionaryCache = dictionaryProvider.GetRequiredService<ICleverCache>();
    Measure("type-invalidate-refill-32-no-notifications", guard, 100, () =>
    {
        dictionaryCache.RemoveByType(typeof(Order));
        for (var i = 0; i < 32; i++)
            dictionaryCache.GetOrCreate(types, $"list-{i}", factory);
    });

    if (ShouldMeasure("mixed-execution-latency"))
    {
        var latencies = new double[10_000];
        Parallel.For(0, latencies.Length, i =>
        {
            var start = Stopwatch.GetTimestamp();
            var key = $"mixed-tail-{i % 32}";
            if (i % 20 == 0) cache.Remove(key);
            cache.GetOrCreate(types, key, factory);
            latencies[i] = Stopwatch.GetElapsedTime(start).TotalNanoseconds;
        });
        Array.Sort(latencies);
        measurements.Add(new Measurement("mixed-execution-latency", guard, latencies[latencies.Length / 2],
            latencies[^1], null, latencies[(int)(latencies.Length * 0.95)], latencies[(int)(latencies.Length * 0.99)]));
    }
}

Console.WriteLine(JsonSerializer.Serialize(measurements, new JsonSerializerOptions { WriteIndented = true }));

void Measure(string name, bool guard, int iterations, Action action, int operationsPerIteration = 1)
{
    if (!ShouldMeasure(name)) return;

    for (var i = 0; i < iterations; i++) action();
    var samples = new List<double>();
    var allocations = new List<double>();
    for (var sample = 0; sample < 9; sample++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < iterations; i++) action();
        var elapsed = Stopwatch.GetElapsedTime(start);
        samples.Add(elapsed.TotalNanoseconds / (iterations * operationsPerIteration));
        allocations.Add((GC.GetTotalAllocatedBytes(precise: true) - allocated) / (double)(iterations * operationsPerIteration));
    }

    samples.Sort();
    allocations.Sort();
    measurements.Add(new Measurement(name, guard, samples[4], samples[8], allocations[4]));
}

bool ShouldMeasure(string name) =>
    args.Length == 0 || args.Any(filter => name.Contains(filter, StringComparison.OrdinalIgnoreCase));

sealed record Measurement(string Scenario, bool Guard, double MedianNsPerOperation, double SlowestSampleNsPerOperation,
    double? AllocatedBytesPerOperation, double? P95Ns = null, double? P99Ns = null);
sealed record OrderQuery(int Id);
sealed class Order;

sealed class DictionaryStore : ICleverCacheStore
{
    private readonly ConcurrentDictionary<object, object> _values = new();
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
    public void Set<TItem>(object key, TItem value, CleverCache.Models.CleverCacheEntryOptions? options = null) =>
        _values[key] = value ?? throw new ArgumentNullException(nameof(value));
    public Task SetAsync<TItem>(object key, TItem value, CleverCache.Models.CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }
    public void Remove(object key) => _values.TryRemove(key, out _);
    public Task RemoveAsync(object key, CancellationToken cancellationToken = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }
}
