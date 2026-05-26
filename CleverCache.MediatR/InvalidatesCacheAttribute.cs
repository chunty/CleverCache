namespace CleverCache.Mediatr;

/// <summary>
/// Marks a MediatR command so that the specified cache types are automatically invalidated
/// after the command handler completes successfully.
/// Cache is only cleared on success — a handler that throws leaves the cache untouched.
/// </summary>
/// <example>
/// <code>
/// [InvalidatesCache(typeof(Order), typeof(OrderLine))]
/// public record DeleteOrderCommand(int OrderId) : IRequest;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class InvalidatesCacheAttribute(params Type[] types) : Attribute
{
	/// <summary>The entity types whose cache entries will be evicted after the command succeeds.</summary>
	public Type[] Types { get; } = types;
}
