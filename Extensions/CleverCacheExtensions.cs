namespace CleverCache.Extensions;
/// <summary>
/// Provides extension methods for the <see cref="ICleverCache"/> interface.
/// </summary>
public static class CleverCacheExtensions
{
	/// <summary>
	/// Adds a dependent cache type.
	/// </summary>
	/// <typeparam name="T">The type of the cache.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="dependentType">The dependent type of the cache.</param>
	public static void AddDependentCache<T>(this ICleverCache cache, Type dependentType) => cache.AddDependentCache(typeof(T), dependentType);

	/// <summary>
	/// Adds the specified key to the cache entry type.
	/// </summary>
	/// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="key">The key of the cache entry to add.</param>
	public static void AddKeyToType<T>(this ICleverCache cache, object key) where T : class => cache.AddKeyToType(typeof(T), key);

	/// <summary>
	/// Adds the specified key to the cache entry type.
	/// </summary>
	/// <param name="cache">The cache instance.</param>
	/// <param name="type">The type of the object the cache key belongs to.</param>
	/// <param name="key">The key of the cache entry to add.</param>
	public static void AddKeyToType(this ICleverCache cache, Type type, object key) => cache.AddKeyToTypes(new[] { type }, key);

	/// <summary>
	/// Gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
	/// </summary>
	/// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
	/// <typeparam name="TItem">The type of the object to get.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="key">The key of the entry to look for or create.</param>
	/// <param name="factory">The factory that creates the value associated with this key if the key does not exist in the cache.</param>
	/// <param name="createOptions">The options to be applied to the <see cref="ICacheEntry"/> if the key does not exist in the cache.</param>
	/// <returns>The value associated with this key.</returns>
	public static TItem? GetOrCreate<T, TItem>(this ICleverCache cache, object key,
		Func<ICacheEntry, TItem> factory,
		MemoryCacheEntryOptions? createOptions = null) where T : class =>
		cache.GetOrCreate(typeof(T), key, factory, createOptions);

	/// <summary>
	/// Gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
	/// </summary>
	/// <typeparam name="TItem">The type of the object to get.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="type">The type of the object the cache key belongs to.</param>
	/// <param name="key">The key of the entry to look for or create.</param>
	/// <param name="factory">The factory that creates the value associated with this key if the key does not exist in the cache.</param>
	/// <param name="createOptions">The options to be applied to the <see cref="ICacheEntry"/> if the key does not exist in the cache.</param>
	/// <returns>The value associated with this key.</returns>
	public static TItem? GetOrCreate<TItem>(this ICleverCache cache, Type type, object key, Func<ICacheEntry, TItem> factory,
		MemoryCacheEntryOptions? createOptions = null) =>
		cache.GetOrCreate(new[] { type }, key, factory, createOptions);

	/// <summary>
	/// Asynchronously gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
	/// </summary>
	/// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
	/// <typeparam name="TItem">The type of the object to get.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="key">The key of the entry to look for or create.</param>
	/// <param name="factory">The factory task that creates the value associated with this key if the key does not exist in the cache.</param>
	/// <param name="createOptions">The options to be applied to the <see cref="ICacheEntry"/> if the key does not exist in the cache.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public static async Task<TItem?> GetOrCreateAsync<T, TItem>(this ICleverCache cache, object key,
		Func<ICacheEntry, Task<TItem>> factory,
		MemoryCacheEntryOptions? createOptions = null) where T : class =>
		await cache.GetOrCreateAsync(typeof(T), key, factory, createOptions);

	/// <summary>
	/// Asynchronously gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
	/// </summary>
	/// <typeparam name="TItem">The type of the object to get.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="type">The type of the object the cache key belongs to.</param>
	/// <param name="key">The key of the entry to look for or create.</param>
	/// <param name="factory">The factory task that creates the value associated with this key if the key does not exist in the cache.</param>
	/// <param name="createOptions">The options to be applied to the <see cref="ICacheEntry"/> if the key does not exist in the cache.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public static async Task<TItem?> GetOrCreateAsync<TItem>(this ICleverCache cache, Type type,
		object key,
		Func<ICacheEntry, Task<TItem>> factory,
		MemoryCacheEntryOptions? createOptions = null) =>
		await cache.GetOrCreateAsync(new[] { type }, key, factory, createOptions);

	/// <summary>
	/// Asynchronously gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
	/// </summary>
	/// <typeparam name="TItem">The type of the object to get.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="types">An array of types the cache key belongs to.</param>
	/// <param name="key">The key of the entry to look for or create.</param>
	/// <param name="factory">The factory task that creates the value associated with this key if the key does not exist in the cache.</param>
	/// <param name="createOptions">The options to be applied to the <see cref="ICacheEntry"/> if the key does not exist in the cache.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public static async Task<TItem?> GetOrCreateAsync<TItem>(this ICleverCache cache, Type[] types,
		object key,
		Func<ICacheEntry, Task<TItem>> factory,
		MemoryCacheEntryOptions? createOptions = null) => throw new NotImplementedException();
}
