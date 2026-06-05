using System.Collections.Concurrent;
using System.Reflection;
using AsyncKeyedLock;
using Microsoft.Extensions.Logging;

namespace CleverCache.Implementations;

/// <inheritdoc cref="ICleverCache"/>
internal class CleverCacheService : CacheEntryManager, ICleverCache
{
	private readonly ICleverCacheStore _store;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<CleverCacheService>? _logger;
	private readonly AsyncKeyedLocker<string> _locker = new();
	private readonly bool _enableAsyncRaceConditionGuard;
	private readonly ConcurrentDictionary<Type, Func<object, ProviderKeyResolution>?> _keyResolvers = new();

	public CleverCacheService(
		ICleverCacheStore store,
		CleverCacheOptions options,
		IServiceProvider? serviceProvider = null,
		ILogger<CleverCacheService>? logger = null)
	{
		_store = store;
		_serviceProvider = serviceProvider ?? NullServiceProvider.Instance;
		_logger = logger;
		_enableAsyncRaceConditionGuard = options.EnableAsyncRaceConditionGuard;
		foreach (var dep in options.DependentCaches)
			AddDependentCache(dep.Type, dep.DependentType);

		if (store is IEvictionNotifyingStore evicting)
			evicting.RegisterEvictionCallback(RemoveKeyFromAllTypes);
	}

	public TItem? GetOrCreate<TItem>(Type[] types, object key, Func<TItem> factory, CleverCacheEntryOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(key);

		if (!TryResolveCanonicalKey(key, out var canonicalKey, out var warningReason))
		{
			_logger?.LogWarning(
				"Skipping cache entry for {KeyType}: {Reason}",
				key.GetType().FullName,
				warningReason ?? "no stable cache key could be produced");
			return factory();
		}

		TItem? CreateAndStore()
		{
			AddCanonicalKeyToTypes(types, canonicalKey);
			var value = factory();
			_store.Set(canonicalKey, value, options);
			return value;
		}

		if (_store.TryGet<TItem>(canonicalKey, out var hit)) return hit;

		if (!_enableAsyncRaceConditionGuard)
			return CreateAndStore();

		using var _ = _locker.Lock(canonicalKey);

		// Double-check: another thread may have populated the cache while we waited for the lock
		if (_store.TryGet<TItem>(canonicalKey, out hit)) return hit;

		return CreateAndStore();
	}

	public async Task<TItem?> GetOrCreateAsync<TItem>(Type[] types, object key, Func<Task<TItem>> factory, CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);

		if (!TryResolveCanonicalKey(key, out var canonicalKey, out var warningReason))
		{
			_logger?.LogWarning(
				"Skipping cache entry for {KeyType}: {Reason}",
				key.GetType().FullName,
				warningReason ?? "no stable cache key could be produced");
			return await factory().ConfigureAwait(false);
		}

		async Task<TItem?> CreateAndStoreAsync()
		{
			AddCanonicalKeyToTypes(types, canonicalKey);
			var value = await factory().ConfigureAwait(false);
			await _store.SetAsync(canonicalKey, value, options, cancellationToken).ConfigureAwait(false);
			return value;
		}

		var (found, cached) = await _store.TryGetAsync<TItem>(canonicalKey, cancellationToken).ConfigureAwait(false);
		if (found) return cached;

		if (!_enableAsyncRaceConditionGuard)
			return await CreateAndStoreAsync().ConfigureAwait(false);

		using var _ = await _locker.LockAsync(canonicalKey, cancellationToken).ConfigureAwait(false);

		// Double-check: another thread may have populated the cache while we waited for the lock
		(found, cached) = await _store.TryGetAsync<TItem>(canonicalKey, cancellationToken).ConfigureAwait(false);
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
		ArgumentNullException.ThrowIfNull(key);

		if (!TryResolveCanonicalKey(key, out var canonicalKey, out var warningReason))
		{
			_logger?.LogWarning(
				"Skipping cache removal for {KeyType}: {Reason}",
				key.GetType().FullName,
				warningReason ?? "no stable cache key could be produced");
			return;
		}

		_store.Remove(canonicalKey);
		RemoveKeyFromAllTypes(canonicalKey);
	}

	public CleverCacheDiagnostics GetDiagnostics() => SnapshotDiagnostics();

	public override void AddKeyToTypes(Type[] types, object key)
	{
		if (!TryResolveCanonicalKey(key, out var canonicalKey, out var warningReason))
		{
			_logger?.LogWarning(
				"Skipping cache key tracking for {KeyType}: {Reason}",
				key.GetType().FullName,
				warningReason ?? "no stable cache key could be produced");
			return;
		}

		AddCanonicalKeyToTypes(types, canonicalKey);
	}

	protected override bool TryResolveCanonicalKey(object key, out string canonicalKey)
		=> TryResolveCanonicalKey(key, out canonicalKey, out _);

	private bool TryResolveCanonicalKey(object key, out string canonicalKey, out string? warningReason)
	{
		var resolvedKey = ResolveKeyValue(key, out var customTypeIdentity);
		if (resolvedKey is null)
		{
			canonicalKey = string.Empty;
			warningReason = null;
			return false;
		}

		if (CacheKeyIdentity.TryGetUnsupportedKeyShapeReason(resolvedKey, out warningReason))
		{
			canonicalKey = string.Empty;
			return false;
		}

		var success = customTypeIdentity is null
			? CacheKeyIdentity.TryToCanonicalKey(resolvedKey, out canonicalKey)
			: CacheKeyIdentity.TryToCanonicalKey(customTypeIdentity, resolvedKey, out canonicalKey);

		if (!success)
		{
			warningReason = null;
			return false;
		}

		warningReason = null;
		return true;
	}

	private object? ResolveKeyValue(object key, out string? customTypeIdentity)
	{
		if (key is string)
		{
			customTypeIdentity = null;
			return key;
		}

		var type = key.GetType();
		var resolver = _keyResolvers.GetOrAdd(type, CreateResolver);
		if (resolver is null)
		{
			customTypeIdentity = null;
			return key;
		}

		var resolution = resolver(key);
		if (resolution.KeyValue is null)
		{
			customTypeIdentity = null;
			return null;
		}

		var sourceTypeIdentity = resolution.SourceType.FullName ?? resolution.SourceType.Name;
		var providerTypeIdentity = resolution.ProviderType.FullName ?? resolution.ProviderType.Name;
		customTypeIdentity = $"{sourceTypeIdentity} + {providerTypeIdentity}";
		return resolution.KeyValue;
	}

	private Func<object, ProviderKeyResolution>? CreateResolver(Type type)
	{
		var providerType = typeof(ICacheKeyProvider<>).MakeGenericType(type);
		var provider = _serviceProvider.GetService(providerType);
		if (provider is null)
			return null;

		var method = typeof(CleverCacheService)
			.GetMethod(nameof(CreateKeyResolver), BindingFlags.NonPublic | BindingFlags.Static)!
			.MakeGenericMethod(type);

		return (Func<object, ProviderKeyResolution>)method.Invoke(null, [provider])!;
	}

	private static Func<object, ProviderKeyResolution> CreateKeyResolver<T>(ICacheKeyProvider<T> provider)
	{
		return value => new ProviderKeyResolution(
			provider.GetKey((T)value),
			typeof(T),
			provider.GetType());
	}

	private sealed record ProviderKeyResolution(object? KeyValue, Type SourceType, Type ProviderType);



	private sealed class NullServiceProvider : IServiceProvider
	{
		public static readonly IServiceProvider Instance = new NullServiceProvider();

		public object? GetService(Type serviceType) => null;
	}
}
