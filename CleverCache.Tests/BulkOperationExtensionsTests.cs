using CleverCache.Extensions;
using Moq;

namespace CleverCache.Tests;

file class BulkEntity;
file class OtherBulkEntity;

public class BulkOperationExtensionsTests
{
    [Fact]
    public void InvalidateCaches_CallsRemoveByTypeForEachType()
    {
        var cacheMock = new Mock<ICleverCache>();

        42.InvalidateCaches(cacheMock.Object, typeof(BulkEntity), typeof(OtherBulkEntity));

        cacheMock.Verify(c => c.RemoveByType(typeof(BulkEntity)), Times.Once);
        cacheMock.Verify(c => c.RemoveByType(typeof(OtherBulkEntity)), Times.Once);
    }

    [Fact]
    public void InvalidateCaches_ReturnsRowCount()
    {
        var cacheMock = new Mock<ICleverCache>();

        var result = 7.InvalidateCaches(cacheMock.Object, typeof(BulkEntity));

        Assert.Equal(7, result);
    }

    [Fact]
    public void InvalidateCaches_Generic_CallsRemoveByTypeForT()
    {
        var cacheMock = new Mock<ICleverCache>();

        5.InvalidateCaches<BulkEntity>(cacheMock.Object);

        cacheMock.Verify(c => c.RemoveByType(typeof(BulkEntity)), Times.Once);
    }

    [Fact]
    public async Task InvalidateCaches_Async_CallsRemoveByTypeForEachType()
    {
        var cacheMock = new Mock<ICleverCache>();

        await Task.FromResult(10).InvalidateCaches(cacheMock.Object, typeof(BulkEntity), typeof(OtherBulkEntity));

        cacheMock.Verify(c => c.RemoveByType(typeof(BulkEntity)), Times.Once);
        cacheMock.Verify(c => c.RemoveByType(typeof(OtherBulkEntity)), Times.Once);
    }

    [Fact]
    public async Task InvalidateCaches_Async_ReturnsRowCount()
    {
        var cacheMock = new Mock<ICleverCache>();

        var result = await Task.FromResult(3).InvalidateCaches(cacheMock.Object, typeof(BulkEntity));

        Assert.Equal(3, result);
    }

    [Fact]
    public async Task InvalidateCaches_Async_Generic_CallsRemoveByTypeForT()
    {
        var cacheMock = new Mock<ICleverCache>();

        await Task.FromResult(1).InvalidateCaches<BulkEntity>(cacheMock.Object);

        cacheMock.Verify(c => c.RemoveByType(typeof(BulkEntity)), Times.Once);
    }
}
