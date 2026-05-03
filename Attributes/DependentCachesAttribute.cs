namespace CleverCache.Attributes;

/// <summary>
/// Declares cache dependency relationships for an entity type.
/// Applied at startup by <c>app.UseCleverCache&lt;TContext&gt;()</c>, which registers the
/// declared dependencies so that invalidating this type also invalidates the dependent types.
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