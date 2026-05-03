using CleverCache.Attributes;
using CleverCache.Implementations;
using Microsoft.Extensions.Caching.Memory;

namespace CleverCache.Tests;

// Test entity types with [DependentCaches] in this assembly
[DependentCaches([typeof(ScanDependent)])]
file class ScanPrimary;
file class ScanDependent;

[DependentCaches([typeof(ScanReverseDependent)], reverse: true)]
file class ScanReverse;
file class ScanReverseDependent;

file class ScanNoAttribute;

public class AssemblyScanningTests
{
    private static ICleverCache BuildCache(CleverCacheOptions options)
    {
        var store = new MemoryCacheStore(new MemoryCache(new MemoryCacheOptions()));
        return new CleverCacheService(store, options);
    }

    [Fact]
    public void ScanAssemblyContaining_RegistersDependentCachesFromAttribute()
    {
        var options = new CleverCacheOptions();
        options.ScanAssemblyContaining<AssemblyScanningTests>();

        Assert.Contains(new DependentCache(typeof(ScanPrimary), typeof(ScanDependent)), options.DependentCaches);
    }

    [Fact]
    public void ScanAssemblyContaining_Reverse_RegistersBothDirections()
    {
        var options = new CleverCacheOptions();
        options.ScanAssemblyContaining<AssemblyScanningTests>();

        Assert.Contains(new DependentCache(typeof(ScanReverse), typeof(ScanReverseDependent)), options.DependentCaches);
        Assert.Contains(new DependentCache(typeof(ScanReverseDependent), typeof(ScanReverse)), options.DependentCaches);
    }

    [Fact]
    public void ScanAssemblyContaining_TypeWithNoAttribute_NotRegistered()
    {
        var options = new CleverCacheOptions();
        options.ScanAssemblyContaining<AssemblyScanningTests>();

        Assert.DoesNotContain(options.DependentCaches, d => d.Type == typeof(ScanNoAttribute));
    }

    [Fact]
    public void ScanAssemblies_MultipleAssemblies_RegistersAll()
    {
        var options = new CleverCacheOptions();
        options.ScanAssemblies(typeof(AssemblyScanningTests).Assembly);

        Assert.Contains(new DependentCache(typeof(ScanPrimary), typeof(ScanDependent)), options.DependentCaches);
    }

    [Fact]
    public void CleverCacheService_InitialisesFromOptions_CascadesWork()
    {
        var options = new CleverCacheOptions();
        options.ScanAssemblyContaining<AssemblyScanningTests>();

        var store = new MemoryCacheStore(new MemoryCache(new MemoryCacheOptions()));
        var cache = new TestCacheManager2(store, options);

        // Adding a key for ScanPrimary should cascade to ScanDependent (wired via [DependentCaches] attribute)
        cache.AddKeyToTypes([typeof(ScanPrimary)], "cascade-key");

        Assert.Contains("cascade-key", cache.KeysFor(typeof(ScanDependent)));
    }
}

// Helper subclass to expose SnapshotKeysFor for the cascade test
file class TestCacheManager2 : CleverCacheService
{
    public TestCacheManager2(ICleverCacheStore store, CleverCacheOptions options) : base(store, options) { }
    public object[] KeysFor(Type type) => SnapshotKeysFor(type);
}
