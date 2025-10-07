namespace CleverCache.Mediatr;
public class AutoCacheAttribute(params Type[] types) : Attribute
{
	public Type[] Types { get; } = types;
}
