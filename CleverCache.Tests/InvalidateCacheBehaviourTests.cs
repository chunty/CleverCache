using CleverCache.Mediatr;
using MediatR;
using Moq;

namespace CleverCache.Tests;

[InvalidatesCache(typeof(InvalidatedEntity), typeof(DependentEntity))]
file record DeleteCommand(int Id) : IRequest<bool>;

file record NoInvalidationCommand(int Id) : IRequest<bool>;

file class InvalidatedEntity;
file class DependentEntity;

public class InvalidateCacheBehaviourTests
{
    [Fact]
    public async Task Handle_NoAttribute_DoesNotInvalidate()
    {
        var cacheMock = new Mock<ICleverCache>();
        var sut = new InvalidateCacheBehaviour<NoInvalidationCommand, bool>(cacheMock.Object);
        RequestHandlerDelegate<bool> next = _ => Task.FromResult(true);

        await sut.Handle(new NoInvalidationCommand(1), next, CancellationToken.None);

        cacheMock.Verify(c => c.RemoveByType(It.IsAny<Type>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAttribute_InvalidatesAllDeclaredTypes()
    {
        var cacheMock = new Mock<ICleverCache>();
        var sut = new InvalidateCacheBehaviour<DeleteCommand, bool>(cacheMock.Object);
        RequestHandlerDelegate<bool> next = _ => Task.FromResult(true);

        await sut.Handle(new DeleteCommand(1), next, CancellationToken.None);

        cacheMock.Verify(c => c.RemoveByType(typeof(InvalidatedEntity)), Times.Once);
        cacheMock.Verify(c => c.RemoveByType(typeof(DependentEntity)), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAttribute_InvalidatesAfterNextCompletes()
    {
        var cacheMock = new Mock<ICleverCache>();
        var order = new List<string>();
        var sut = new InvalidateCacheBehaviour<DeleteCommand, bool>(cacheMock.Object);

        cacheMock.Setup(c => c.RemoveByType(It.IsAny<Type>()))
            .Callback<Type>(_ => order.Add("invalidate"));

        RequestHandlerDelegate<bool> next = _ =>
        {
            order.Add("handler");
            return Task.FromResult(true);
        };

        await sut.Handle(new DeleteCommand(1), next, CancellationToken.None);

        Assert.Equal(["handler", "invalidate", "invalidate"], order);
    }

    [Fact]
    public async Task Handle_WithAttribute_ReturnsHandlerResult()
    {
        var cacheMock = new Mock<ICleverCache>();
        var sut = new InvalidateCacheBehaviour<DeleteCommand, bool>(cacheMock.Object);
        RequestHandlerDelegate<bool> next = _ => Task.FromResult(true);

        var result = await sut.Handle(new DeleteCommand(1), next, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task Handle_HandlerThrows_DoesNotInvalidate()
    {
        var cacheMock = new Mock<ICleverCache>();
        var sut = new InvalidateCacheBehaviour<DeleteCommand, bool>(cacheMock.Object);
        RequestHandlerDelegate<bool> next = _ => Task.FromException<bool>(new InvalidOperationException("db error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Handle(new DeleteCommand(1), next, CancellationToken.None));

        cacheMock.Verify(c => c.RemoveByType(It.IsAny<Type>()), Times.Never);
    }
}
