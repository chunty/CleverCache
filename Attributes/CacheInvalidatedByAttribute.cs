namespace CleverCache.Attributes;

/// <summary>
/// Declares which entity changes invalidate cached data for the decorated entity.
/// Register these relationships at startup with <c>ScanAssemblyContaining&lt;T&gt;()</c>.
/// Apply multiple instances to configure a separate <see cref="Reverse"/> setting for each group of types.
/// </summary>
/// <example>
/// <code>
/// // Changes to OrderLine or OrderNote invalidate Order caches.
/// [CacheInvalidatedBy(typeof(OrderLine), typeof(OrderNote))]
/// public class Order { }
///
/// // Changes to either type invalidate caches for both.
/// [CacheInvalidatedBy(typeof(OrderLine), Reverse = true)]
/// public class Order { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class CacheInvalidatedByAttribute(
	params Type[] types
) : Attribute
{
	/// <summary>The entity types whose changes also invalidate caches for the decorated entity.</summary>
	public Type[] DependentTypes { get; } = types ?? [];

	/// <summary>Registers both directions without requiring declarations on both entities.</summary>
	public bool Reverse { get; set; }
}
