using AsyncKeyedLock;

namespace CleverCache.Implementations;

/// <inheritdoc cref="ICleverCache"/>
internal class CleverCacheService : CacheEntryManager, ICleverCache
{
	private readonly ICleverCacheStore _store;
	private readonly AsyncKeyedLocker<object> _locker = new();
	private readonly bool _enableAsyncRaceConditionGuard;

	public CleverCacheService(ICleverCacheStore store, CleverCacheOptions options)
	{
		_store = store;
		_enableAsyncRaceConditionGuard = options.EnableAsyncRaceConditionGuard;
		foreach (var dep in options.DependentCaches)
			AddDependentCache(dep.Type, dep.DependentType);

		if (store is IEvictionNotifyingStore evicting)
			evicting.RegisterEvictionCallback(RemoveKeyFromAllTypes);
	}

	public TItem? GetOrCreate<TItem>(Type[] types, object key, Func<TItem> factory, CleverCacheEntryOptions? options = null)
	{
		TItem? CreateAndStore()
		{
			AddKeyToTypes(types, key);
			var value = factory();
			_store.Set(key, value, options);
			return value;
		}

		if (_store.TryGet<TItem>(key, out var hit)) return hit;

		if (!_enableAsyncRaceConditionGuard)
			return CreateAndStore();

		using var _ = _locker.Lock(key);

		// Double-check: another thread may have populated the cache while we waited for the lock
		if (_store.TryGet<TItem>(key, out hit)) return hit;

		return CreateAndStore();
	}

	public async Task<TItem?> GetOrCreateAsync<TItem>(Type[] types, object key, Func<Task<TItem>> factory, CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
	{
		async Task<TItem?> CreateAndStoreAsync()
		{
			AddKeyToTypes(types, key);
			var value = await factory().ConfigureAwait(false);
			await _store.SetAsync(key, value, options, cancellationToken).ConfigureAwait(false);
			return value;
		}

		var (found, cached) = await _store.TryGetAsync<TItem>(key, cancellationToken).ConfigureAwait(false);
		if (found) return cached;

		if (!_enableAsyncRaceConditionGuard)
			return await CreateAndStoreAsync().ConfigureAwait(false);

		using var _ = await _locker.LockAsync(key, cancellationToken).ConfigureAwait(false);

		// Double-check: another thread may have populated the cache while we waited for the lock
		(found, cached) = await _store.TryGetAsync<TItem>(key, cancellationToken).ConfigureAwait(false);
		if (found) return cached;

		return await CreateAndStoreAsync().ConfigureAwait(false);
	}

	public void RemoveByType(Type type)
	{
		foreach (var k in SnapshotKeysFor(type))
		{
			_store.Remove(k);
			RemoveKeyFromAllTypes(k);
		}
	}

	public async Task RemoveByTypeAsync(Type type, CancellationToken cancellationToken = default)
	{
		foreach (var k in SnapshotKeysFor(type))
		{
			await _store.RemoveAsync(k, cancellationToken).ConfigureAwait(false);
			RemoveKeyFromAllTypes(k);
		}
	}

	public void Remove(object key)
	{
		_store.Remove(key);
		RemoveKeyFromAllTypes(key);
	}

	public CleverCacheDiagnostics GetDiagnostics() => SnapshotDiagnostics();
}
