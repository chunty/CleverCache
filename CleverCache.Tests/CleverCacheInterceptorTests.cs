using CleverCache.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CleverCache.Tests;

internal class IcOrder { public int Id { get; set; } public string Name { get; set; } = ""; }
internal class IcCustomer { public int Id { get; set; } }

internal class IcDbContext(DbContextOptions<IcDbContext> options) : DbContext(options)
{
    public DbSet<IcOrder> Orders => Set<IcOrder>();
    public DbSet<IcCustomer> Customers => Set<IcCustomer>();
}

public class CleverCacheInterceptorTests
{
    private static (IcDbContext context, Mock<ICleverCache> mockCache) CreateContext()
    {
        var mockCache = new Mock<ICleverCache>();
        var interceptor = new CleverCacheInterceptor(mockCache.Object);
        var options = new DbContextOptionsBuilder<IcDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        return (new IcDbContext(options), mockCache);
    }

    [Fact]
    public void SavedChanges_EntityAdded_CallsRemoveByType()
    {
        var (context, mockCache) = CreateContext();
        context.Orders.Add(new IcOrder { Id = 1, Name = "Test" });
        context.SaveChanges();

        mockCache.Verify(c => c.RemoveByType(typeof(IcOrder)), Times.Once);
    }

    [Fact]
    public void SavedChanges_EntityModified_CallsRemoveByType()
    {
        var (context, mockCache) = CreateContext();
        context.Orders.Add(new IcOrder { Id = 1, Name = "Test" });
        context.SaveChanges();
        mockCache.Invocations.Clear();

        var order = context.Orders.Find(1)!;
        order.Name = "Updated";
        context.SaveChanges();

        mockCache.Verify(c => c.RemoveByType(typeof(IcOrder)), Times.Once);
    }

    [Fact]
    public void SavedChanges_EntityDeleted_CallsRemoveByType()
    {
        var (context, mockCache) = CreateContext();
        context.Orders.Add(new IcOrder { Id = 1, Name = "Test" });
        context.SaveChanges();
        mockCache.Invocations.Clear();

        context.Orders.Remove(context.Orders.Find(1)!);
        context.SaveChanges();

        mockCache.Verify(c => c.RemoveByType(typeof(IcOrder)), Times.Once);
    }

    [Fact]
    public void SavedChanges_MultipleEntitiesOfSameType_CallsRemoveByTypeOnce()
    {
        var (context, mockCache) = CreateContext();
        context.Orders.AddRange(
            new IcOrder { Id = 1, Name = "A" },
            new IcOrder { Id = 2, Name = "B" });
        context.SaveChanges();

        mockCache.Verify(c => c.RemoveByType(typeof(IcOrder)), Times.Once);
    }

    [Fact]
    public void SavedChanges_MultipleEntityTypes_CallsRemoveByTypeForEach()
    {
        var (context, mockCache) = CreateContext();
        context.Orders.Add(new IcOrder { Id = 1, Name = "Test" });
        context.Customers.Add(new IcCustomer { Id = 1 });
        context.SaveChanges();

        mockCache.Verify(c => c.RemoveByType(typeof(IcOrder)), Times.Once);
        mockCache.Verify(c => c.RemoveByType(typeof(IcCustomer)), Times.Once);
    }

    [Fact]
    public void SavedChanges_NoChanges_DoesNotCallRemoveByType()
    {
        var (context, mockCache) = CreateContext();
        context.SaveChanges();

        mockCache.Verify(c => c.RemoveByType(It.IsAny<Type>()), Times.Never);
    }

    [Fact]
    public async Task SavedChangesAsync_EntityAdded_CallsRemoveByType()
    {
        var (context, mockCache) = CreateContext();
        context.Orders.Add(new IcOrder { Id = 1, Name = "Test" });
        await context.SaveChangesAsync();

        mockCache.Verify(c => c.RemoveByType(typeof(IcOrder)), Times.Once);
    }
}
