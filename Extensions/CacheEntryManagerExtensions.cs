namespace CleverCache.Extensions;

/// <summary>
/// Provides extension methods for <see cref="ICleverCache"/>.
/// </summary>
public static class CacheEntryManagerExtensions
{
	/// <summary>
	/// Registers that cached data for <typeparamref name="T"/> depends on <paramref name="dependentType"/>.
	/// Invalidating <paramref name="dependentType"/> also invalidates cache entries for <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The entity type whose cached data depends on <paramref name="dependentType"/>.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="dependentType">The type whose invalidation should also evict cache entries for <typeparamref name="T"/>.</param>
	public static void AddDependentCache<T>(this ICleverCache cache, Type dependentType) => cache.AddDependentCache(typeof(T), dependentType);

	/// <summary>
	/// Registers a cache key under the specified entity type so that the key is automatically
	/// evicted when data of type <typeparamref name="T"/> changes.
	/// </summary>
	/// <typeparam name="T">The entity type to associate with this cache key.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="key">The cache key to register.</param>
	public static void AddKeyToType<T>(this ICleverCache cache, object key) where T : class => cache.AddKeyToType(typeof(T), key);

	/// <summary>
	/// Registers a cache key under the specified entity type so that the key is automatically
	/// evicted when data of that type changes.
	/// </summary>
	/// <param name="cache">The cache instance.</param>
	/// <param name="type">The entity type to associate with this cache key.</param>
	/// <param name="key">The cache key to register.</param>
	public static void AddKeyToType(this ICleverCache cache, Type type, object key) => cache.AddKeyToTypes([type], key);

	/// <summary>
	/// Asynchronously removes all cache entries associated with <typeparamref name="T"/>,
	/// including entries for types whose cached data depends on it.
	/// Prefer over <c>RemoveByType</c> when using a distributed cache backend.
	/// </summary>
	public static Task RemoveByTypeAsync<T>(this ICleverCache cache, CancellationToken cancellationToken = default) =>
		cache.RemoveByTypeAsync(typeof(T), cancellationToken);
}
