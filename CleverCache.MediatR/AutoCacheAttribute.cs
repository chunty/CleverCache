namespace CleverCache.Mediatr;

/// <summary>
/// Marks a MediatR query so that its result is automatically cached by the CleverCache pipeline behaviour.
/// The MediatR request object is used as the cache key, so the same query with different parameters
/// gets its own cache entry.
/// Cache entries are evicted automatically when any of the specified entity types changes via EF Core.
/// </summary>
/// <example>
/// <code>
/// [AutoCache([typeof(Order)])]
/// public record GetOrdersQuery(int CustomerId) : IRequest&lt;List&lt;Order&gt;&gt;;
/// </code>
/// </example>
public class AutoCacheAttribute(params Type[] types) : Attribute
{
	/// <summary>The entity types this cache entry is associated with. The entry is evicted when any of these types is invalidated.</summary>
	public Type[] Types { get; } = types;
}
