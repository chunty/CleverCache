using CleverCache.EntityFrameworkCore.Exceptions;
using CleverCache.EntityFrameworkCore.Extensions;
using CleverCache.EntityFrameworkCore.Interceptors;
using CleverCache.EntityFrameworkCore.Models;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CleverCache.Tests;

// Navigation model: DbExt prefix avoids any collision with other test types
internal class DbExtOrder { public int Id { get; set; } public List<DbExtOrderLine> Lines { get; set; } = []; }
internal class DbExtOrderLine { public int Id { get; set; } public int DbExtOrderId { get; set; } public DbExtOrder? Order { get; set; } }

// Recursive model: A → B → C
internal class DbExtA { public int Id { get; set; } public DbExtB? B { get; set; } }
internal class DbExtB { public int Id { get; set; } public DbExtC? C { get; set; } }
internal class DbExtC { public int Id { get; set; } }

internal class ScanTestDbContext(DbContextOptions<ScanTestDbContext> options) : DbContext(options)
{
    public DbSet<DbExtOrder> Orders => Set<DbExtOrder>();
    public DbSet<DbExtOrderLine> OrderLines => Set<DbExtOrderLine>();
    public DbSet<DbExtA> As => Set<DbExtA>();
    public DbSet<DbExtB> Bs => Set<DbExtB>();
    public DbSet<DbExtC> Cs => Set<DbExtC>();
}

public class DbContextExtensionsTests
{
    private static ScanTestDbContext CreateContext(bool withInterceptor = false)
    {
        var builder = new DbContextOptionsBuilder<ScanTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());
        if (withInterceptor)
            builder.AddInterceptors(new CleverCacheInterceptor(new Mock<ICleverCache>().Object));
        return new ScanTestDbContext(builder.Options);
    }

    [Fact]
    public void EnsureCleverCacheInterceptor_WithInterceptor_DoesNotThrow()
    {
        using var context = CreateContext(withInterceptor: true);
        context.EnsureCleverCacheInterceptor(); // should not throw
    }

    [Fact]
    public void EnsureCleverCacheInterceptor_WithoutInterceptor_ThrowsMissingInterceptorException()
    {
        using var context = CreateContext(withInterceptor: false);
        Assert.Throws<MissingInterceptorException>(() => context.EnsureCleverCacheInterceptor());
    }

    [Fact]
    public void DiscoverDependentCaches_DirectNavigation_DiscoversRelationship()
    {
        using var context = CreateContext();
        var result = context.DiscoverDependentCaches(new CleverCacheScanOptions(DependentCacheNavigationScanMode.Direct));

        Assert.Contains(result, d => d.Type == typeof(DbExtOrder) && d.DependentType == typeof(DbExtOrderLine));
    }

    [Fact]
    public void DiscoverDependentCaches_NoneMode_ReturnsEmpty()
    {
        using var context = CreateContext();
        var result = context.DiscoverDependentCaches(new CleverCacheScanOptions(DependentCacheNavigationScanMode.None));

        Assert.Empty(result);
    }

    [Fact]
    public void DiscoverDependentCaches_RecursiveNavigation_DiscoversTransitiveRelationships()
    {
        using var context = CreateContext();
        var result = context.DiscoverDependentCaches(new CleverCacheScanOptions(DependentCacheNavigationScanMode.Recursive));

        Assert.Contains(result, d => d.Type == typeof(DbExtA) && d.DependentType == typeof(DbExtB));
        Assert.Contains(result, d => d.Type == typeof(DbExtB) && d.DependentType == typeof(DbExtC));
    }
}

