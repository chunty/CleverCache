namespace CleverCache.Tests;

// Concrete subclass so we can test the abstract CacheEntryManager via its public API
file class TestCacheManager : CacheEntryManager
{
    public string[] KeysFor(Type type) => SnapshotKeysFor(type);
    public CleverCacheDiagnostics Diagnostics() => SnapshotDiagnostics();
}

public class CacheEntryManagerTests
{
    [Fact]
    public void AddKeyToTypes_AssociatesKeyWithType()
    {
        var mgr = new TestCacheManager();
        var expected = CacheKeyIdentity.ToCanonicalKey("myKey");

        mgr.AddKeyToTypes([typeof(string)], "myKey");

        Assert.Contains(expected, mgr.KeysFor(typeof(string)));
    }

    [Fact]
    public void AddKeyToTypes_MultipleTypes_KeyAssociatedWithAll()
    {
        var mgr = new TestCacheManager();
        var expected = CacheKeyIdentity.ToCanonicalKey("sharedKey");

        mgr.AddKeyToTypes([typeof(string), typeof(int)], "sharedKey");

        Assert.Contains(expected, mgr.KeysFor(typeof(string)));
        Assert.Contains(expected, mgr.KeysFor(typeof(int)));
    }

    [Fact]
    public void AddDependentCache_KeyForPrimaryAlsoRegisteredUnderDependent()
    {
        var mgr = new TestCacheManager();
        mgr.AddDependentCache(typeof(string), typeof(int));
        var expected = CacheKeyIdentity.ToCanonicalKey("key1");

        mgr.AddKeyToTypes([typeof(string)], "key1");

        Assert.Contains(expected, mgr.KeysFor(typeof(string)));
        Assert.Contains(expected, mgr.KeysFor(typeof(int)));
    }

    [Fact]
    public void AddKeyToTypes_TransitiveDependency_KeyRegisteredUnderAllTypes()
    {
        // A -> B -> C: key for A should appear under A, B, and C
        var mgr = new TestCacheManager();
        mgr.AddDependentCache(typeof(string), typeof(int));
        mgr.AddDependentCache(typeof(int), typeof(bool));
        var expected = CacheKeyIdentity.ToCanonicalKey("transitiveKey");

        mgr.AddKeyToTypes([typeof(string)], "transitiveKey");

        Assert.Contains(expected, mgr.KeysFor(typeof(string)));
        Assert.Contains(expected, mgr.KeysFor(typeof(int)));
        Assert.Contains(expected, mgr.KeysFor(typeof(bool)));
    }

    [Fact]
    public void AddKeyToTypes_CyclicDependency_DoesNotInfiniteLoop()
    {
        // A -> B -> A: should terminate cleanly
        var mgr = new TestCacheManager();
        mgr.AddDependentCache(typeof(string), typeof(int));
        mgr.AddDependentCache(typeof(int), typeof(string));
        var expected = CacheKeyIdentity.ToCanonicalKey("cycleKey");

        var ex = Record.Exception(() => mgr.AddKeyToTypes([typeof(string)], "cycleKey"));

        Assert.Null(ex);
        Assert.Contains(expected, mgr.KeysFor(typeof(string)));
        Assert.Contains(expected, mgr.KeysFor(typeof(int)));
    }

    [Fact]
    public void SnapshotKeysFor_UnknownType_ReturnsEmpty()
    {
        var mgr = new TestCacheManager();

        Assert.Empty(mgr.KeysFor(typeof(double)));
    }

    [Fact]
    public void SnapshotDiagnostics_ReflectsDependantsAndKeys()
    {
        var mgr = new TestCacheManager();
        mgr.AddDependentCache(typeof(string), typeof(int));
        mgr.AddKeyToTypes([typeof(string)], "k1");

        var d = mgr.Diagnostics();
        var expected = CacheKeyIdentity.ToCanonicalKey("k1");

        Assert.True(d.Dependants.ContainsKey(typeof(string)));
        Assert.Contains(typeof(int), d.Dependants[typeof(string)]);
        Assert.True(d.KeysByType.ContainsKey(typeof(string)));
        Assert.Contains(expected, d.KeysByType[typeof(string)]);
        // int inherits the key via cascade expansion
        Assert.Contains(expected, d.KeysByType[typeof(int)]);
    }
}
