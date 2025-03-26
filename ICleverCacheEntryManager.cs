namespace CleverCache;

public interface ICleverCacheEntryManager
{
	/// <summary>
	/// Adds a dependent cache type.
	/// </summary>
	/// <param name="type">The type of the cache.</param>
	/// <param name="dependentType">The dependent type of the cache.</param>
	void AddDependentCache(Type type, Type dependentType);

	/// <summary>
	/// Adds the specified key to the cache entry type.
	/// </summary>
	/// <param name="types">An array types the cache key belongs to.</param>
	/// <param name="key">The key of the cache entry to add.</param>
	void AddKeyToTypes(Type[] types, object key);
}