namespace CleverCache;

public interface ICacheKeyProvider<in T>
{
	object GetKey(T value);
}
