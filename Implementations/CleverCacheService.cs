using AsyncKeyedLock;

namespace CleverCache.Implementations;

/// <inheritdoc cref="ICleverCache"/>
internal class CleverCacheService(ICleverCacheStore store) : CacheEntryManager, ICleverCache
{
	private readonly AsyncKeyedLocker<object> _locker = new();

	public TItem? GetOrCreate<TItem>(Type[] types, object key, Func<TItem> factory, CleverCacheEntryOptions? options = null)
	{
		if (store.TryGet<TItem>(key, out var hit)) return hit;

		using var _ = _locker.Lock(key);

		// Double-check: another thread may have populated the cache while we waited for the lock
		if (store.TryGet<TItem>(key, out hit)) return hit;

		AddKeyToTypes(types, key);
		var value = factory();
		store.Set(key, value, options);
		return value;
	}

	public async Task<TItem?> GetOrCreateAsync<TItem>(Type[] types, object key, Func<Task<TItem>> factory, CleverCacheEntryOptions? options = null)
	{
		var (found, cached) = await store.TryGetAsync<TItem>(key).ConfigureAwait(false);
		if (found) return cached;

		using var _ = await _locker.LockAsync(key).ConfigureAwait(false);

		// Double-check: another thread may have populated the cache while we waited for the lock
		(found, cached) = await store.TryGetAsync<TItem>(key).ConfigureAwait(false);
		if (found) return cached;

		AddKeyToTypes(types, key);
		var value = await factory().ConfigureAwait(false);
		await store.SetAsync(key, value, options).ConfigureAwait(false);
		return value;
	}

	public void RemoveByType(Type type)
	{
		foreach (var k in SnapshotKeysFor(type))
		{
			store.Remove(k);
		}
	}

	public void Remove(object key) => store.Remove(key);
}

