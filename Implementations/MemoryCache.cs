namespace CleverCache.Implementations;

/// <inheritdoc cref="ICleverCache"/>
public class CleverMemoryCache(IMemoryCache memoryCache) : CacheEntryManager, ICleverCache
{
	public TItem? GetOrCreate<TItem>(Type[] types, object key, Func<ICacheEntry, TItem> factory, MemoryCacheEntryOptions? options = null)
	{
		if (memoryCache.TryGetValue(key, out var hit)) return (TItem?)hit;

		using var entry = memoryCache.CreateEntry(key);
		if (options is not null) entry.SetOptions(options);

		// Track (adds key to all types+dependents) + auto-untrack on eviction
		TrackWithEviction(entry, types, key);

		var value = factory(entry);
		entry.Value = value;
		return (TItem?)value;
	}

	public async Task<TItem?> GetOrCreateAsync<TItem>(Type[] types, object key, Func<ICacheEntry, Task<TItem>> factory, MemoryCacheEntryOptions? options = null)
	{
		if (memoryCache.TryGetValue(key, out var hit)) return (TItem?)hit;

		using var entry = memoryCache.CreateEntry(key);
		if (options is not null) entry.SetOptions(options);
		
		var value = await factory(entry).ConfigureAwait(false);
		entry.Value = value;
		return (TItem?)value;
	}

	public void RemoveByType(Type type)
	{
		var keys = SnapshotKeysFor(type);      // snapshot avoids races
		foreach (var k in keys)
		{
			memoryCache.Remove(k);
			UntrackKeyFor(type, k);            // optional; eviction callback also clears
		}
	}

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
		TrackWithEviction(result, types, key);
		return result;
	}
}
