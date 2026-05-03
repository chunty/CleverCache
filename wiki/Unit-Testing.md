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
