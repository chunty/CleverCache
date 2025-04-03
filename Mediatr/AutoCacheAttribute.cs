namespace CleverCache.Mediatr;
public class AutoCacheAttribute(Type[] types) : Attribute
{
	public Type[] Types { get; } = types;
}
