using CleverCache.Mediatr;
using MediatR;
using Moq;

namespace CleverCache.Tests;

[AutoCache([typeof(CachedEntity)])]
file record CachedQuery(int Id) : IRequest<string>;

file record UncachedQuery(int Id) : IRequest<string>;

file class CachedEntity;

public class AutoCacheBehaviourTests
{
    [Fact]
    public async Task Handle_NoAttribute_AlwaysCallsNext()
    {
        var cacheMock = new Mock<ICleverCache>();
        var sut = new AutoCacheBehaviour<UncachedQuery, string>(cacheMock.Object);
        var callCount = 0;
        RequestHandlerDelegate<string> next = _ => { callCount++; return Task.FromResult("result"); };

        await sut.Handle(new UncachedQuery(1), next, CancellationToken.None);
        await sut.Handle(new UncachedQuery(1), next, CancellationToken.None);

        Assert.Equal(2, callCount);
        cacheMock.Verify(c => c.GetOrCreateAsync(
            It.IsAny<Type[]>(), It.IsAny<object>(), It.IsAny<Func<Task<string>>>(), It.IsAny<CleverCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAttribute_CacheMiss_CallsNextAndCaches()
    {
        var cacheMock = new Mock<ICleverCache>();
        cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<Type[]>(), It.IsAny<object>(), It.IsAny<Func<Task<string>>>(), It.IsAny<CleverCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns<Type[], object, Func<Task<string?>>, CleverCacheEntryOptions?, CancellationToken>((_, _, factory, _, _) => factory());

        var sut = new AutoCacheBehaviour<CachedQuery, string>(cacheMock.Object);
        var callCount = 0;
        RequestHandlerDelegate<string> next = _ => { callCount++; return Task.FromResult("fresh"); };

        var result = await sut.Handle(new CachedQuery(1), next, CancellationToken.None);

        Assert.Equal("fresh", result);
        Assert.Equal(1, callCount);
        cacheMock.Verify(c => c.GetOrCreateAsync(
            It.IsAny<Type[]>(), It.IsAny<object>(), It.IsAny<Func<Task<string>>>(), It.IsAny<CleverCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAttribute_CacheHit_DoesNotCallNext()
    {
        var cacheMock = new Mock<ICleverCache>();
        cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<Type[]>(), It.IsAny<object>(), It.IsAny<Func<Task<string>>>(), It.IsAny<CleverCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cached-value"); // returns cached directly, never invokes factory

        var sut = new AutoCacheBehaviour<CachedQuery, string>(cacheMock.Object);
        var callCount = 0;
        RequestHandlerDelegate<string> next = _ => { callCount++; return Task.FromResult("fresh"); };

        var result = await sut.Handle(new CachedQuery(1), next, CancellationToken.None);

        Assert.Equal("cached-value", result);
        Assert.Equal(0, callCount);
    }
}
