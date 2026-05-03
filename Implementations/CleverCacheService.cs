using AsyncKeyedLock;

namespace CleverCache.Implementations;

/// <inheritdoc cref="ICleverCache"/>
internal class CleverCacheService : CacheEntryManager, ICleverCache
{
	private readonly ICleverCacheStore _store;
	private readonly AsyncKeyedLocker<object> _locker = new();

	public CleverCacheService(ICleverCacheStore store, CleverCacheOptions options)
	{
		_store = store;
		foreach (var dep in options.DependentCaches)
			AddDependentCache(dep.Type, dep.DependentType);
	}

	public TItem? GetOrCreate<TItem>(Type[] types, object key, Func<TItem> factory, CleverCacheEntryOptions? options = null)
	{
		if (_store.TryGet<TItem>(key, out var hit)) return hit;

		using var _ = _locker.Lock(key);

		// Double-check: another thread may have populated the cache while we waited for the lock
		if (_store.TryGet<TItem>(key, out hit)) return hit;

		AddKeyToTypes(types, key);
		var value = factory();
		_store.Set(key, value, options);
		return value;
	}

	public async Task<TItem?> GetOrCreateAsync<TItem>(Type[] types, object key, Func<Task<TItem>> factory, CleverCacheEntryOptions? options = null)
	{
		var (found, cached) = await _store.TryGetAsync<TItem>(key).ConfigureAwait(false);
		if (found) return cached;

		using var _ = await _locker.LockAsync(key).ConfigureAwait(false);

		// Double-check: another thread may have populated the cache while we waited for the lock
		(found, cached) = await _store.TryGetAsync<TItem>(key).ConfigureAwait(false);
		if (found) return cached;

		AddKeyToTypes(types, key);
		var value = await factory().ConfigureAwait(false);
		await _store.SetAsync(key, value, options).ConfigureAwait(false);
		return value;
	}

	public void RemoveByType(Type type)
	{
		foreach (var k in SnapshotKeysFor(type))
			_store.Remove(k);
	}

	public void Remove(object key) => _store.Remove(key);
}

