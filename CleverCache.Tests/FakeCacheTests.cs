using CleverCache.Implementations;

namespace CleverCache.Tests;

public class FakeCacheTests
{
    [Fact]
    public void GetOrCreate_AlwaysCallsFactory()
    {
        var fake = new FakeCache();
        var callCount = 0;

        fake.GetOrCreate([typeof(string)], "key", () => { callCount++; return 1; });
        fake.GetOrCreate([typeof(string)], "key", () => { callCount++; return 2; });

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetOrCreateAsync_AlwaysCallsFactory()
    {
        var fake = new FakeCache();
        var callCount = 0;

        await fake.GetOrCreateAsync([typeof(string)], "key", async () => { callCount++; await Task.Yield(); return 1; });
        await fake.GetOrCreateAsync([typeof(string)], "key", async () => { callCount++; await Task.Yield(); return 2; });

        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Remove_DoesNotThrow()
    {
        var fake = new FakeCache();
        var ex = Record.Exception(() => fake.Remove("any-key"));
        Assert.Null(ex);
    }

    [Fact]
    public void RemoveByType_DoesNotThrow()
    {
        var fake = new FakeCache();
        var ex = Record.Exception(() => fake.RemoveByType(typeof(string)));
        Assert.Null(ex);
    }

    [Fact]
    public void AddKeyToTypes_DoesNotThrow()
    {
        var fake = new FakeCache();
        var ex = Record.Exception(() => fake.AddKeyToTypes([typeof(string)], "key"));
        Assert.Null(ex);
    }
}
