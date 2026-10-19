namespace CleverCache.Attributes;

/// <summary>
/// Declares cache dependency relationships for an entity type.
/// Use <c>builder.Services.AddCleverCache(o => o.ScanAssemblyContaining&lt;T&gt;())</c> to register
/// these at startup. Cached data for the decorated entity depends on the listed types:
/// invalidating any listed type also invalidates cache entries for the decorated entity.
/// </summary>
/// <example>
/// <code>
/// // Order caches depend on OrderLine and OrderNote.
/// // Invalidating either listed type also invalidates Order caches.
/// [DependentCaches([typeof(OrderLine), typeof(OrderNote)])]
/// public class Order { }
///
/// // reverse: true also invalidates OrderLine caches when Order changes.
/// // Declare this on one entity instead of adding matching attributes to both.
/// [DependentCaches([typeof(OrderLine)], reverse: true)]
/// public class Order { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class)]
//[Obsolete("Use CacheInvalidatedByAttribute instead. Dependency behavior is unchanged.")]
public class DependentCachesAttribute : CacheInvalidatedByAttribute
{
	public DependentCachesAttribute(Type[] types, bool reverse = false) : base(types)
	{
		Reverse = reverse;
	}
}