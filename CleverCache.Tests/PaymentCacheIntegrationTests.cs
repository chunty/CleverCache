using CleverCache.EntityFrameworkCore.Interceptors;
using CleverCache.Implementations;
using CleverCache.Mediatr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CleverCache.Tests;

[AutoCache(typeof(IcOrder))]
file record OrderDetailQuery(int Id);

[AutoCache(typeof(IcOrder))]
file record OrderListQuery;

public class PaymentCacheIntegrationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PaymentThenWon_EfInvalidationRefreshesDetailAndList(bool distributed, bool guard)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var distributedMemory = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        ICleverCacheStore store = distributed
            ? new DistributedCacheStore(distributedMemory)
            : new MemoryCacheStore(memory);
        var cache = new CleverCacheService(store, new CleverCacheOptions { EnableAsyncRaceConditionGuard = guard });
        var options = new DbContextOptionsBuilder<IcDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new CleverCacheInterceptor(cache))
            .Options;
        await using var context = new IcDbContext(options);
        var cancellationToken = TestContext.Current.CancellationToken;
        var order = new IcOrder { Id = 1, Name = "AwaitingPayment" };
        context.Orders.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        var detail = new AutoCacheBehaviour<OrderDetailQuery, string>(cache);
        var list = new AutoCacheBehaviour<OrderListQuery, string>(cache);
        var detailReads = 0;
        var listReads = 0;

        async Task<string> ReadDetail() => await detail.Handle(new OrderDetailQuery(1), async ct =>
        {
            detailReads++;
            return await context.Orders.AsNoTracking().Where(x => x.Id == 1).Select(x => x.Name).SingleAsync(ct);
        }, cancellationToken);

        async Task<string> ReadList() => await list.Handle(new OrderListQuery(), async ct =>
        {
            listReads++;
            return string.Join(",", await context.Orders.AsNoTracking().Select(x => x.Name).ToListAsync(ct));
        }, cancellationToken);

        foreach (var status in new[] { "AwaitingPayment", "Paid", "Won" })
        {
            order.Name = status;
            await context.SaveChangesAsync(cancellationToken);
            Assert.Equal(status, await ReadDetail());
            Assert.Equal(status, await ReadList());
            Assert.Equal(status, await ReadDetail());
            Assert.Equal(status, await ReadList());
        }

        Assert.Equal(3, detailReads);
        Assert.Equal(3, listReads);
        foreach (var key in cache.GetDiagnostics().KeysByType.Values.SelectMany(keys => keys).Distinct())
            cache.Remove(key);

        Assert.Equal("Won", await ReadDetail());
        Assert.Equal("Won", await ReadList());
        Assert.Equal(4, detailReads);
        Assert.Equal(4, listReads);
    }
}
