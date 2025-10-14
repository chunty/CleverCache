namespace CleverCache.Implementations;

/// <inheritdoc cref="ICleverCache"/>
public class CleverMemoryCache(IMemoryCache memoryCache) : CacheEntryManager, ICleverCache
{
	public TItem? GetOrCreate<TItem>(Type[] types, object key, Func<ICacheEntry, TItem> factory, MemoryCacheEntryOptions? options = null)
	{
		if (memoryCache.TryGetValue(key, out var hit)) return (TItem?)hit;

		using var entry = GetCacheEntry(types, key, options);
		return SetEntryValue(factory(entry), entry);
	}

	public async Task<TItem?> GetOrCreateAsync<TItem>(Type[] types, object key, Func<ICacheEntry, Task<TItem>> factory, MemoryCacheEntryOptions? options = null)
	{
		if (memoryCache.TryGetValue(key, out var hit)) return (TItem?)hit;

		using var entry = GetCacheEntry(types, key, options);
		return SetEntryValue(await factory(entry).ConfigureAwait(false), entry);
	}

	public void RemoveByType(Type type)
	{
		// snapshot avoids races
		foreach (var k in SnapshotKeysFor(type))
		{
			memoryCache.Remove(k);
		}
	}

	public void Remove(object key) => memoryCache.Remove(key);

	private static TItem? SetEntryValue<TItem>(TItem value, ICacheEntry entry)
	{
		entry.Value = value;
		return (TItem?)entry.Value;
	}

	private ICacheEntry GetCacheEntry(Type[] types, object key, MemoryCacheEntryOptions? options)
	{
		ICacheEntry? entry = null;
		try
		{
			AddKeyToTypes(types, key);
			entry = memoryCache.CreateEntry(key);
			if (options is not null) entry.SetOptions(options);
			return entry;
		}
		catch
		{
			entry?.Dispose();
			throw;
		}
	}

}
