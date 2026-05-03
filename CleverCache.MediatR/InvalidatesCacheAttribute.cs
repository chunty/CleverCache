namespace CleverCache.Mediatr;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class InvalidatesCacheAttribute(params Type[] types) : Attribute
{
	public Type[] Types { get; } = types;
}
