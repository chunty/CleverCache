namespace CleverCache.Attributes;

/// <summary>
/// Declares cache dependency relationships for an entity type.
/// Use <c>builder.Services.AddCleverCache(o => o.ScanAssemblyContaining&lt;T&gt;())</c> to register
/// these at startup, or pick them up automatically via <c>app.ScanDbSetsForCacheDependencies&lt;TContext&gt;()</c>
/// if you are using EF Core navigation scanning.
/// </summary>
/// <example>
/// <code>
/// // Invalidating Order also invalidates OrderLine and OrderNote
/// [DependentCaches([typeof(OrderLine), typeof(OrderNote)])]
/// public class Order { }
///
/// // reverse: true also invalidates Order when OrderLine changes
/// [DependentCaches([typeof(OrderLine)], reverse: true)]
/// public class Order { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class)]
public class DependentCachesAttribute(
	Type[] types,
	DependentCacheNavigationScanMode navigationScanMode = DependentCacheNavigationScanMode.None,
	bool reverse = false
) : Attribute
{
	/// <summary>The entity types that should also be invalidated when this type's entries are evicted.</summary>
	public Type[] DependantTypes { get; } = types ?? [];

	/// <summary>Controls whether navigation properties are scanned to discover additional dependent types.</summary>
	public DependentCacheNavigationScanMode NavigationScanMode { get; set; } = navigationScanMode;

	/// <summary>When <c>true</c>, also registers the inverse dependency so that invalidating any dependent type also invalidates this type.</summary>
	public bool Reverse { get; set; } = reverse;
}