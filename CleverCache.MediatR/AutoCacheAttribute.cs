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
public class AutoCacheAttribute(params Type[] invalidatedBy) : Attribute
{
	/// <summary>The types whose invalidation evicts this query's cached result, including registered dependency cascades. These are not the query's result types.</summary>
	public Type[] InvalidatedBy { get; } = invalidatedBy;

	/// <summary>Compatibility alias for <see cref="InvalidatedBy"/>.</summary>
	[Obsolete("Use InvalidatedBy instead. Cache invalidation behavior is unchanged.")]
	public Type[] Types => InvalidatedBy;

	/// <summary>Optional sliding expiration in seconds for this request key.</summary>
	public int SlidingExpirationSeconds { get; set; } = -1;

	/// <summary>Optional absolute expiration in seconds from now for this request key.</summary>
	public int AbsoluteExpirationSeconds { get; set; } = -1;
}
