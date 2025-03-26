namespace CleverCache.Implementations;

/// <inheritdoc cref="ICleverCache"/>
public class CleverMemoryCache(IMemoryCache memoryCache) : CacheEntryManager, ICleverCache
{
	private readonly SemaphoreSlim _semaphore = new(1, 1);

	
	/// <inheritdoc />
	public TItem? GetOrCreate<TItem>(Type[] types, object key, Func<ICacheEntry, TItem> factory, MemoryCacheEntryOptions? createOptions = null)
	{
		if (memoryCache.TryGetValue(key, out var result))
		{
			return (TItem?)result;
		}

		try
		{
			// Prevent race conditions
			_semaphore.Wait();

			using var entry = CreateEntry(types, key);

			if (createOptions != null)
			{
				entry.SetOptions(createOptions);
			}

			result = factory(entry);
			entry.Value = result;

			return (TItem?)result;
		}
		finally
		{
			_semaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<TItem?> GetOrCreateAsync<TItem>(Type[] types,
		object key,
		Func<ICacheEntry, Task<TItem>> factory,
		MemoryCacheEntryOptions? createOptions = null)
	{
		if (memoryCache.TryGetValue(key, out var result))
		{
			return (TItem?)result;
		}

		try
		{
			// Prevent race conditions
			await _semaphore.WaitAsync();

			using var entry = CreateEntry(types, key);

			if (createOptions != null)
			{
				entry.SetOptions(createOptions);
			}

			result = await factory(entry).ConfigureAwait(false);
			entry.Value = result;

			return (TItem?)result;
		}
		finally
		{
			_semaphore.Release();
		}
	}


	/// <inheritdoc />
	public void RemoveByType(Type type)
	{
		foreach (var entry in CacheEntries.Where(x => x.Type == type))
		{
			Remove(entry.Key);
		}
	}

	/// <inheritdoc />
	public void Remove(object key) => memoryCache.Remove(key);


	/* Private methods */

	/// <summary>
	/// Creates a cache entry for the specified types and key.
	/// </summary>
	/// <param name="types">An array of types the cache key belongs to.</param>
	/// <param name="key">The key of the cache entry to create.</param>
	/// <returns>The created cache entry.</returns>
	private ICacheEntry CreateEntry(Type[] types, object key)
	{
		var result = memoryCache.CreateEntry(key);
		AddKeyToTypes(types, key);
		return result;
	}
}
