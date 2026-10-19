using CleverCache.Attributes;
using CleverCache.Implementations;
using Microsoft.Extensions.Caching.Memory;

namespace CleverCache.Tests;

#pragma warning disable CS0618 // Verify compatibility with the obsolete attribute.
[DependentCaches([typeof(ScanDependent)])]
file class ScanPrimary;
file class ScanDependent;

[DependentCaches([typeof(ScanReverseDependent)], reverse: true)]
file class ScanReverse;
file class ScanReverseDependent;

[DependentCaches([typeof(ScanDependent)])]
[CacheInvalidatedBy(typeof(ScanReverseDependent), Reverse = true)]
file class ScanMixed;
#pragma warning restore CS0618

[CacheInvalidatedBy(typeof(ScanDependent))]
file class ScanNew;

[CacheInvalidatedBy(typeof(ScanReverseDependent), Reverse = true)]
file class ScanNewReverse;

[CacheInvalidatedBy(typeof(ScanDependent), typeof(ScanReverseDependent))]
file class ScanMultiple;

[CacheInvalidatedBy([typeof(ScanDependent)])]
file class ScanArray;

[CacheInvalidatedBy(typeof(ScanOneWay))]
[CacheInvalidatedBy(typeof(ScanTwoWay), Reverse = true)]
file class ScanRepeated;
file class ScanOneWay;
file class ScanTwoWay;

file class ScanNoAttribute;

public class AssemblyScanningTests
{
    [Fact]
    public void ScanAssemblyContaining_RepeatedAttributes_PreserveIndividualReverseSettings()
    {
        var options = new CleverCacheOptions().ScanAssemblyContaining<AssemblyScanningTests>();

        Assert.Contains(new DependentCache(typeof(ScanRepeated), typeof(ScanOneWay)), options.DependentCaches);
        Assert.DoesNotContain(new DependentCache(typeof(ScanOneWay), typeof(ScanRepeated)), options.DependentCaches);
        Assert.Contains(new DependentCache(typeof(ScanRepeated), typeof(ScanTwoWay)), options.DependentCaches);
        Assert.Contains(new DependentCache(typeof(ScanTwoWay), typeof(ScanRepeated)), options.DependentCaches);
    }

    [Theory]
    [InlineData(typeof(ScanRepeated), 2, 1, 2)]
    [InlineData(typeof(ScanOneWay), 2, 2, 2)]
    [InlineData(typeof(ScanTwoWay), 2, 1, 2)]
    public void RepeatedAttributes_InvalidateAccordingToIndividualReverseSettings(
        Type changedType, int expectedEntityReads, int expectedOneWayReads, int expectedTwoWayReads)
    {
        var cache = BuildCache(new CleverCacheOptions().ScanAssemblyContaining<AssemblyScanningTests>());
        var entityReads = 0;
        var oneWayReads = 0;
        var twoWayReads = 0;
        int ReadEntity() => cache.GetOrCreate([typeof(ScanRepeated)], "entity", () => ++entityReads);
        int ReadOneWay() => cache.GetOrCreate([typeof(ScanOneWay)], "one-way", () => ++oneWayReads);
        int ReadTwoWay() => cache.GetOrCreate([typeof(ScanTwoWay)], "two-way", () => ++twoWayReads);

        Assert.Equal(1, ReadEntity());
        Assert.Equal(1, ReadOneWay());
        Assert.Equal(1, ReadTwoWay());

        cache.RemoveByType(changedType);

        Assert.Equal(expectedEntityReads, ReadEntity());
        Assert.Equal(expectedOneWayReads, ReadOneWay());
        Assert.Equal(expectedTwoWayReads, ReadTwoWay());
    }

    [Fact]
    public void ScanAssemblyContaining_ParamsAndArray_RegisterDependencies()
    {
        var options = new CleverCacheOptions().ScanAssemblyContaining<AssemblyScanningTests>();

        Assert.Contains(new DependentCache(typeof(ScanMultiple), typeof(ScanDependent)), options.DependentCaches);
        Assert.Contains(new DependentCache(typeof(ScanMultiple), typeof(ScanReverseDependent)), options.DependentCaches);
        Assert.Contains(new DependentCache(typeof(ScanArray), typeof(ScanDependent)), options.DependentCaches);
    }

    [Fact]
    public void CacheInvalidatedBy_Params_PreservesDefaultsAndPropertyInitializer()
    {
        var attribute = new CacheInvalidatedByAttribute(typeof(ScanDependent), typeof(ScanReverseDependent));

        Assert.Equal(new[] { typeof(ScanDependent), typeof(ScanReverseDependent) }, attribute.DependentTypes);
        Assert.False(attribute.Reverse);
        Assert.Empty(new CacheInvalidatedByAttribute().DependentTypes);
        Assert.True(new CacheInvalidatedByAttribute(typeof(ScanDependent)) { Reverse = true }.Reverse);
    }

    [Fact]
    public void ScanAssemblyContaining_RegistersNewAttributeAndReverse()
    {
        var options = new CleverCacheOptions().ScanAssemblyContaining<AssemblyScanningTests>();

        Assert.Contains(new DependentCache(typeof(ScanNew), typeof(ScanDependent)), options.DependentCaches);
        Assert.DoesNotContain(new DependentCache(typeof(ScanDependent), typeof(ScanNew)), options.DependentCaches);
        Assert.Contains(new DependentCache(typeof(ScanNewReverse), typeof(ScanReverseDependent)), options.DependentCaches);
        Assert.Contains(new DependentCache(typeof(ScanReverseDependent), typeof(ScanNewReverse)), options.DependentCaches);
    }

    [Fact]
    public void ScanAssemblyContaining_MixedAttributes_RegistersBoth()
    {
        var options = new CleverCacheOptions().ScanAssemblyContaining<AssemblyScanningTests>();

        Assert.Contains(new DependentCache(typeof(ScanMixed), typeof(ScanDependent)), options.DependentCaches);
        Assert.Contains(new DependentCache(typeof(ScanMixed), typeof(ScanReverseDependent)), options.DependentCaches);
        Assert.Contains(new DependentCache(typeof(ScanReverseDependent), typeof(ScanMixed)), options.DependentCaches);
    }

    [Theory]
    [InlineData(typeof(ScanPrimary), false)]
    [InlineData(typeof(ScanReverse), true)]
    [InlineData(typeof(ScanNew), false)]
    [InlineData(typeof(ScanNewReverse), true)]
    public void Attributes_PreserveInvalidationDirection(Type entityType, bool reverse)
    {
        var options = new CleverCacheOptions().ScanAssemblyContaining<AssemblyScanningTests>();
        var dependency = reverse ? typeof(ScanReverseDependent) : typeof(ScanDependent);
        var cache = BuildCache(options);
        var entityReads = 0;
        var dependencyReads = 0;
        int ReadEntity() => cache.GetOrCreate([entityType], "entity", () => ++entityReads);
        int ReadDependency() => cache.GetOrCreate([dependency], "dependency", () => ++dependencyReads);

        Assert.Equal(1, ReadEntity());
        Assert.Equal(1, ReadDependency());
        cache.RemoveByType(entityType);
        Assert.Equal(2, ReadEntity());
        Assert.Equal(reverse ? 2 : 1, ReadDependency());
        cache.RemoveByType(dependency);
        Assert.Equal(3, ReadEntity());
        Assert.Equal(reverse ? 3 : 2, ReadDependency());
    }

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
        var expected = CacheKeyIdentity.ToCanonicalKey("cascade-key");

        // Adding a key for ScanPrimary should cascade to ScanDependent (wired via [DependentCaches] attribute)
        cache.AddKeyToTypes([typeof(ScanPrimary)], "cascade-key");

        Assert.Contains(expected, cache.KeysFor(typeof(ScanDependent)));
    }
}

// Helper subclass to expose SnapshotKeysFor for the cascade test
file class TestCacheManager2 : CleverCacheService
{
    public TestCacheManager2(ICleverCacheStore store, CleverCacheOptions options) : base(store, options) { }
    public string[] KeysFor(Type type) => SnapshotKeysFor(type);
}
