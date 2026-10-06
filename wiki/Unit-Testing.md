# Unit Testing

## Making cache transparent with `FakeCache`

CleverCache ships with a `FakeCache` implementation that never caches — it always calls through to the underlying factory. This makes your service logic fully testable without the cache getting in the way.

```csharp
using CleverCache;

var mocker = new AutoMocker();
mocker.Use<ICleverCache>(new FakeCache());
var sut = mocker.CreateInstance<OrderService>();

// Tests run as normal — the factory is always called, cache is invisible
var result = await sut.GetAllOrdersAsync();
```

`FakeCache` is in the root `CleverCache` namespace — no additional `using` directives needed beyond what you'd normally import.

## Verifying cache interactions with `Mock<ICleverCache>`

If you need to assert *how* the cache was used — for example, that the factory was called exactly once, or that a specific key was evicted — use a mock instead:

```csharp
var cacheMock = new Mock<ICleverCache>();
cacheMock
    .Setup(c => c.GetOrCreateAsync(
        It.IsAny<Type[]>(),
        It.IsAny<object>(),
        It.IsAny<Func<Task<List<Order>>>>(),
        null))
    .ReturnsAsync(new List<Order>());

var sut = new OrderService(cacheMock.Object, db);
await sut.GetAllOrdersAsync();

cacheMock.Verify(c => c.GetOrCreateAsync(...), Times.Once);
```

`ICleverCache` is a plain interface and works with any mocking library (Moq, NSubstitute, FakeItEasy, etc.).

## MediatR and `[AutoCache]`

If you are *only* using MediatR automatic caching via `[AutoCache]` and never injecting `ICleverCache` directly into your services, you don't need `FakeCache` at all — the `AutoCacheBehaviour` pipeline behaviour is never part of your unit test boundary.

For integration tests that exercise the full MediatR pipeline, register `FakeCache` or an in-memory store in your test service collection.

## Invalidation regression tests and performance measurements

`CacheInvalidationRaceTests` uses explicit gates to reproduce invalidation during query execution/store publication and delayed eviction after replacement. Tests cover both stampede-guard settings and assert fresh subsequent reads, rather than asserting the presence of the bug.

Run the regressions:

```powershell
dotnet test .\CleverCache.Tests\CleverCache.Tests.csproj -f net10.0 --filter "FullyQualifiedName~CacheInvalidationRaceTests"
```

`PaymentCacheIntegrationTests` exercises separate detail/list cache keys through the MediatR caching behaviour and actual EF save interception, with memory and distributed-memory stores.

Run the Release-mode measurement harness:

```powershell
dotnet run --project .\CleverCache.Benchmarks\CleverCache.Benchmarks.csproj -c Release -f net10.0
# Limit measured scenarios, for example to hot hits:
dotnet run --project .\CleverCache.Benchmarks\CleverCache.Benchmarks.csproj -c Release -f net10.0 -- hit
```

The harness reports nine timed samples per throughput scenario, median/slowest sample nanoseconds per operation and process-wide allocations. The separate mixed execution-latency scenario reports per-operation p95/p99 (excluding scheduler queueing). Compare repeated runs on the same idle machine; this lightweight harness is not a production load-test or a substitute for controlled benchmarking.

The memory-cache invalidation baseline can appear artificially fast when the broken implementation has lost its tracking and skips removals. The `type-invalidate-refill-32-no-notifications` scenario uses a dictionary store without callbacks to compare real removal/refill work on both implementations.
