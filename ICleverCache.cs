namespace CleverCache;

public interface ICleverCache : ICacheEntryManager
{
	/// <summary>
	/// Gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
	/// </summary>
	/// <param name="types">An array types the cache key belongs to.</param>
	/// <param name="key">The key of the entry to look for or create.</param>
	/// <param name="factory">The factory that creates the value associated with this key if the key does not exist in the cache.</param>
	/// <param name="createOptions">The options to be applied to the <see cref="ICacheEntry"/> if the key does not exist in the cache.</param>
	/// <returns>The value associated with this key.</returns>
	TItem? GetOrCreate<TItem>(
		Type[] types,
		object key,
		Func<ICacheEntry, TItem> factory,
		MemoryCacheEntryOptions? createOptions = null);

	/// <summary>
	/// Asynchronously gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
	/// </summary>
	/// <typeparam name="TItem">The type of the object to get.</typeparam>
	/// <param name="types">An array types the cache key belongs to.</param>
	/// <param name="key">The key of the entry to look for or create.</param>
	/// <param name="factory">The factory task that creates the value associated with this key if the key does not exist in the cache.</param>
	/// <param name="createOptions">The options to be applied to the <see cref="ICacheEntry"/> if the key does not exist in the cache.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	Task<TItem?> GetOrCreateAsync<TItem>(
		Type[] types,
		object key,
		Func<ICacheEntry, Task<TItem>> factory,
		MemoryCacheEntryOptions? createOptions = null);

	/// <summary>
	/// Removes the object associated with the given key.
	/// </summary>
	/// <param name="key">An object identifying the entry.</param>
	void Remove(object key);

	/// <summary>
	/// Removes all cache entries of the specified type.
	/// </summary>
	/// <param name="type">The type of the objects to remove cache entries for.</param>
	void RemoveByType(Type type);

}