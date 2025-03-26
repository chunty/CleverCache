namespace CleverCache.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="ICacheEntryManager"/> interface.
/// </summary>
public static class CacheEntryManagerExtensions
{
	/// <summary>
	/// Adds a dependent cache type.
	/// </summary>
	/// <typeparam name="T">The type of the cache.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="dependentType">The dependent type of the cache.</param>
	public static void AddDependentCache<T>(this ICacheEntryManager cache, Type dependentType) => cache.AddDependentCache(typeof(T), dependentType);

	/// <summary>
	/// Adds the specified key to the cache entry type.
	/// </summary>
	/// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="key">The key of the cache entry to add.</param>
	public static void AddKeyToType<T>(this ICacheEntryManager cache, object key) where T : class => cache.AddKeyToType(typeof(T), key);

	/// <summary>
	/// Adds the specified key to the cache entry type.
	/// </summary>
	/// <param name="cache">The cache instance.</param>
	/// <param name="type">The type of the object the cache key belongs to.</param>
	/// <param name="key">The key of the cache entry to add.</param>
	public static void AddKeyToType(this ICacheEntryManager cache, Type type, object key) => cache.AddKeyToTypes([type], key);
}
