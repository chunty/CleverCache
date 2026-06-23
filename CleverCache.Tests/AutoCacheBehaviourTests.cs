using CleverCache.Mediatr;
using MediatR;
using Moq;

namespace CleverCache.Tests;

[AutoCache([typeof(CachedEntity)])]
file record CachedQuery(int Id) : IRequest<string>;

[AutoCache([typeof(CachedEntity)], SlidingExpirationSeconds = 1800)]
file record SlidingCachedQuery(int Id) : IRequest<string>;

[AutoCache([typeof(CachedEntity)], AbsoluteExpirationSeconds = 1800)]
file record AbsoluteCachedQuery(int Id) : IRequest<string>;

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

    [Fact]
    public async Task Handle_WithAttribute_NullResult_DoesNotCallNextTwice()
    {
        var cacheMock = new Mock<ICleverCache>();
        cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<Type[]>(), It.IsAny<object>(), It.IsAny<Func<Task<string?>>>(), It.IsAny<CleverCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns<Type[], object, Func<Task<string?>>, CleverCacheEntryOptions?, CancellationToken>((_, _, factory, _, _) => factory());

        var sut = new AutoCacheBehaviour<CachedQuery, string>(cacheMock.Object);
        var callCount = 0;
        RequestHandlerDelegate<string> next = _ => { callCount++; return Task.FromResult<string>(null!); };

        var result = await sut.Handle(new CachedQuery(1), next, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, callCount); // handler must only execute once even when result is null
    }

    [Fact]
    public async Task Handle_WithAttribute_SlidingExpiration_UsesAttributeValue()
    {
        var cacheMock = new Mock<ICleverCache>();
        CleverCacheEntryOptions? capturedOptions = null;
        cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<Type[]>(), It.IsAny<object>(), It.IsAny<Func<Task<string>>>(), It.IsAny<CleverCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns<Type[], object, Func<Task<string>>, CleverCacheEntryOptions?, CancellationToken>((_, _, factory, options, _) =>
            {
                capturedOptions = options;
                return factory().ContinueWith(task => (string?)task.Result, TaskScheduler.Default);
            });

        var sut = new AutoCacheBehaviour<SlidingCachedQuery, string>(cacheMock.Object);

        await sut.Handle(new SlidingCachedQuery(1), _ => Task.FromResult("fresh"), CancellationToken.None);

        Assert.NotNull(capturedOptions);
        Assert.Equal(TimeSpan.FromMinutes(30), capturedOptions!.SlidingExpiration);
        Assert.Null(capturedOptions.AbsoluteExpiration);
    }

    [Fact]
    public async Task Handle_WithAttribute_AbsoluteExpiration_UsesAttributeValue()
    {
        var cacheMock = new Mock<ICleverCache>();
        CleverCacheEntryOptions? capturedOptions = null;
        cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<Type[]>(), It.IsAny<object>(), It.IsAny<Func<Task<string>>>(), It.IsAny<CleverCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns<Type[], object, Func<Task<string>>, CleverCacheEntryOptions?, CancellationToken>((_, _, factory, options, _) =>
            {
                capturedOptions = options;
                return factory().ContinueWith(task => (string?)task.Result, TaskScheduler.Default);
            });

        var sut = new AutoCacheBehaviour<AbsoluteCachedQuery, string>(cacheMock.Object);

        await sut.Handle(new AbsoluteCachedQuery(1), _ => Task.FromResult("fresh"), CancellationToken.None);

        Assert.NotNull(capturedOptions);
        Assert.NotNull(capturedOptions!.AbsoluteExpiration);
        Assert.True(capturedOptions.AbsoluteExpiration > DateTimeOffset.UtcNow.AddMinutes(29));
        Assert.True(capturedOptions.AbsoluteExpiration < DateTimeOffset.UtcNow.AddMinutes(31));
        Assert.Null(capturedOptions.SlidingExpiration);
    }
}
