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
	private readonly AsyncKeyedLocker<string> _storeOperations = new();
	private readonly ConcurrentDictionary<string, EntryState> _entries = new();
	private readonly bool _enableAsyncRaceConditionGuard;
	private readonly ConcurrentDictionary<Type, Func<object, ProviderKeyResolution>?> _keyResolvers = new();
	private readonly CleverCacheEntryOptions _defaultEntryOptions;

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
		_defaultEntryOptions = CloneOptions(options.DefaultEntryOptions ?? new CleverCacheEntryOptions
		{
			SlidingExpiration = TimeSpan.FromHours(4)
		});
		foreach (var dep in options.DependentCaches)
			AddDependentCache(dep.Type, dep.DependentType);

		if (store is IEvictionNotifyingStore evicting)
			evicting.RegisterEvictionCallback(OnEvicted);
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

		if (_store.TryGet<TItem>(canonicalKey, out var hit)) return hit;

		if (!_enableAsyncRaceConditionGuard)
			return CreateAndStore(types, canonicalKey, factory, options);

		using var _ = _locker.Lock(canonicalKey);

		// Double-check: another thread may have populated the cache while we waited for the lock
		if (_store.TryGet<TItem>(canonicalKey, out hit)) return hit;

		return CreateAndStore(types, canonicalKey, factory, options);
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

		var (found, cached) = await _store.TryGetAsync<TItem>(canonicalKey, cancellationToken).ConfigureAwait(false);
		if (found) return cached;

		if (!_enableAsyncRaceConditionGuard)
			return await CreateAndStoreAsync(types, canonicalKey, factory, options, cancellationToken).ConfigureAwait(false);

		using var _ = await _locker.LockAsync(canonicalKey, cancellationToken).ConfigureAwait(false);

		// Double-check: another thread may have populated the cache while we waited for the lock
		(found, cached) = await _store.TryGetAsync<TItem>(canonicalKey, cancellationToken).ConfigureAwait(false);
		if (found) return cached;

		return await CreateAndStoreAsync(types, canonicalKey, factory, options, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Runs a cache-miss factory and stores its result only if the key has not been invalidated meanwhile.
	/// </summary>
	/// <remarks>
	/// Registration and publication use the per-key store-operation lock, but the factory runs outside it.
	/// An invalidated result is still returned to its original caller; it is not cached or retried.
	/// Every registered fill is ended exactly once, including when the factory or store throws.
	/// </remarks>
	private TItem? CreateAndStore<TItem>(Type[] types, string key, Func<TItem> factory, CleverCacheEntryOptions? options)
	{
		Fill fill;
		using (_storeOperations.Lock(key))
			fill = BeginFill(types, key);

		var ended = false;
		try
		{
			var value = factory();
			using (_storeOperations.Lock(key))
			{
				try
				{
					if (CanPublish(fill))
						_store.Set(key, value, ResolveCreateOptions(options));
				}
				finally
				{
					ended = true;
					EndFill(key, fill.State);
				}
			}
			return value;
		}
		finally
		{
			if (!ended)
			{
				using (_storeOperations.Lock(key))
					EndFill(key, fill.State);
			}
		}
	}

	/// <summary>
	/// Runs an asynchronous cache-miss factory and publishes its result only while its generation is valid.
	/// </summary>
	/// <remarks>
	/// The factory runs without the store-operation lock. Publication holds that per-key lock across
	/// the generation check and the awaited store write, closing the gap in which removal could otherwise
	/// finish before an old write. Invalidation can wait for that write, but never for the query factory.
	/// Cleanup does not use the caller's cancellation token, so cancellation cannot strand an active fill.
	/// </remarks>
	private async Task<TItem?> CreateAndStoreAsync<TItem>(Type[] types, string key, Func<Task<TItem>> factory,
		CleverCacheEntryOptions? options, CancellationToken cancellationToken)
	{
		Fill fill;
		using (await _storeOperations.LockAsync(key, cancellationToken).ConfigureAwait(false))
			fill = BeginFill(types, key);

		var ended = false;
		try
		{
			var value = await factory().ConfigureAwait(false);
			using (await _storeOperations.LockAsync(key, cancellationToken).ConfigureAwait(false))
			{
				try
				{
					if (CanPublish(fill))
						await _store.SetAsync(key, value, ResolveCreateOptions(options), cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					ended = true;
					EndFill(key, fill.State);
				}
			}
			return value;
		}
		finally
		{
			// Cleanup must also run when the caller's token has been cancelled.
			if (!ended)
			{
				using (await _storeOperations.LockAsync(key).ConfigureAwait(false))
					EndFill(key, fill.State);
			}
		}
	}

	public void RemoveByType(Type type)
	{
		foreach (var k in SnapshotKeysFor(type))
		{
			RemoveCanonicalKey(k);
		}
	}

	public async Task RemoveByTypeAsync(Type type, CancellationToken cancellationToken = default)
	{
		foreach (var k in SnapshotKeysFor(type))
		{
			using (await _storeOperations.LockAsync(k, cancellationToken).ConfigureAwait(false))
			{
				var state = InvalidateFill(k);
				await _store.RemoveAsync(k, cancellationToken).ConfigureAwait(false);
				CompleteRemoval(k, state);
			}
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

		RemoveCanonicalKey(canonicalKey);
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

		using (_storeOperations.Lock(canonicalKey))
		{
			var fill = BeginFill(types, canonicalKey);
			lock (fill.State)
				fill.State.HasEntry = true;
			EndFill(canonicalKey, fill.State);
		}
	}

	/// <summary>
	/// Registers an active fill under its entity types and captures the key's current invalidation generation.
	/// </summary>
	/// <remarks>
	/// The caller must hold the per-key store-operation lock. The state lock also protects against
	/// eviction callbacks retiring the state concurrently. If a callback already retired the selected
	/// state, registration retries against the current identity before the factory starts.
	/// </remarks>
	private Fill BeginFill(Type[] types, string key)
	{
		while (true)
		{
			var state = _entries.GetOrAdd(key, static _ => new EntryState());
			lock (state)
			{
				if (!IsCurrent(key, state)) continue;
				state.ActiveFactories++;
				AddCanonicalKeyToTypes(types, key);
				return new Fill(state, state.Generation);
			}
		}
	}

	/// <summary>
	/// Checks whether a fill survived invalidation and reserves tracking for its impending store write.
	/// </summary>
	/// <returns>False if removal advanced the generation after this fill began.</returns>
	/// <remarks>
	/// The caller must hold the per-key store-operation lock through the subsequent store write.
	/// This check alone is not atomic publication. HasEntry is set conservatively before writing:
	/// a store may write successfully and then throw, in which case the entry must remain clearable.
	/// </remarks>
	private static bool CanPublish(Fill fill)
	{
		lock (fill.State)
		{
			if (fill.State.Generation != fill.Generation) return false;

			// Keep the entry tracked even if a store writes and then throws.
			fill.State.HasEntry = true;
			return true;
		}
	}

	/// <summary>
	/// Releases one active fill and reconciles any eviction notification deferred during factory execution.
	/// </summary>
	/// <remarks>
	/// The caller must hold the per-key store-operation lock and call this exactly once per BeginFill.
	/// The final active fill checks the store if an eviction was deferred; a live replacement keeps
	/// its registrations, while an absent entry allows unused state and tracking to be retired.
	/// </remarks>
	private void EndFill(string key, EntryState state)
	{
		lock (state)
		{
			state.ActiveFactories--;
			if (state.ActiveFactories == 0 && state.EvictionPending)
			{
				state.HasEntry = _store.TryGet<object>(key, out _);
				state.EvictionPending = false;
			}
			RemoveUnusedState(key, state);
		}
	}

	/// <summary>
	/// Invalidates active fills, removes the stored value, and retires unused tracking for an already canonical key.
	/// </summary>
	/// <remarks>
	/// Serializes with publication for this key, not with factories or unrelated keys.
	/// Tracking cleanup happens only after successful store removal. If removal throws, the exception
	/// propagates and tracking remains available for a later attempt.
	/// </remarks>
	private void RemoveCanonicalKey(string key)
	{
		using (_storeOperations.Lock(key))
		{
			var state = InvalidateFill(key);
			_store.Remove(key);
			CompleteRemoval(key, state);
		}
	}

	/// <summary>
	/// Advances the key's generation so factories registered before removal can no longer publish.
	/// </summary>
	/// <returns>The state to reconcile after removal, or null when the key has no coordination state.</returns>
	/// <remarks>
	/// The caller must hold the per-key store-operation lock. This changes validity without waiting
	/// for factories, deleting their state, or removing the stored value itself.
	/// </remarks>
	private EntryState? InvalidateFill(string key)
	{
		if (!_entries.TryGetValue(key, out var state)) return null;
		lock (state)
			state.Generation++;
		return state;
	}

	/// <summary>
	/// Records successful store removal and drops tracking when no active fill still needs the state.
	/// </summary>
	/// <remarks>
	/// The caller must hold the per-key store-operation lock and invoke this only after removal succeeds.
	/// Active fills retain their state and registrations until EndFill, preserving the generation
	/// they must check before attempting publication.
	/// </remarks>
	private void CompleteRemoval(string key, EntryState? state)
	{
		if (state is null) return;
		lock (state)
		{
			state.HasEntry = false;
			RemoveUnusedState(key, state);
		}
	}

	/// <summary>
	/// Reconciles a store eviction hint without letting an old notification untrack a newer entry.
	/// </summary>
	/// <remarks>
	/// Notifications identify only a key and may arrive late or synchronously inside a store operation.
	/// This method therefore uses the state lock, not the store-operation lock, avoiding reentrant
	/// deadlock. Active fills defer the existence check to EndFill; otherwise a current stored value,
	/// including a cached null, keeps its tracking. Notifications for retired states are ignored.
	/// </remarks>
	private void OnEvicted(object key)
	{
		var canonicalKey = key is string s && CacheKeyIdentity.IsCanonicalKey(s)
			? s
			: CacheKeyIdentity.ToCanonicalKey(key);
		if (!_entries.TryGetValue(canonicalKey, out var state)) return;

		lock (state)
		{
			if (!IsCurrent(canonicalKey, state)) return;
			if (state.ActiveFactories > 0)
			{
				state.EvictionPending = true;
				return;
			}

			// A key-only notification may belong to an older entry. Never untrack a live replacement.
			state.HasEntry = _store.TryGet<object>(canonicalKey, out _);
			RemoveUnusedState(canonicalKey, state);
		}
	}

	/// <summary>
	/// Checks identity, not generation, to distinguish this coordination state from a replacement for the same key.
	/// </summary>
	/// <remarks>
	/// Call while holding the supplied state's lock when using the result to modify tracking.
	/// </remarks>
	private bool IsCurrent(string key, EntryState state) =>
		_entries.TryGetValue(key, out var current) && ReferenceEquals(current, state);

	/// <summary>
	/// Retires the current state only when it owns neither a stored entry nor an active fill.
	/// </summary>
	/// <remarks>
	/// The caller must hold the state's lock. Type registrations are removed before the identity
	/// leaves the dictionary: reversing that order would let a new fill register a replacement
	/// whose registrations could then be erased by this cleanup.
	/// </remarks>
	private void RemoveUnusedState(string key, EntryState state)
	{
		if (state.ActiveFactories != 0 || state.HasEntry || !IsCurrent(key, state)) return;
		// Keep this identity visible until its registrations are gone, so a new fill cannot lose them.
		RemoveKeyFromAllTypes(key);
		_entries.TryRemove(key, out _);
	}

	/// <summary>
	/// Mutable coordination state shared by fills and eviction callbacks for one canonical key.
	/// All field access is protected by locking this instance.
	/// </summary>
	private sealed class EntryState
	{
		public long Generation;
		public int ActiveFactories;
		public bool HasEntry;
		public bool EvictionPending;
	}

	/// <summary>
	/// Captures the state identity and generation at registration; removal changes the state's
	/// generation, making this snapshot ineligible for publication.
	/// </summary>
	private readonly record struct Fill(EntryState State, long Generation);

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

	private CleverCacheEntryOptions ResolveCreateOptions(CleverCacheEntryOptions? options)
	{
		return HasExplicitExpiration(options) ? 
			options! : 
			CloneOptions(_defaultEntryOptions);
	}

	private static bool HasExplicitExpiration(CleverCacheEntryOptions? options) =>
		options is not null &&
		(options.AbsoluteExpiration is not null ||
		 options.AbsoluteExpirationRelativeToNow is not null ||
		 options.SlidingExpiration is not null);

	private static CleverCacheEntryOptions CloneOptions(CleverCacheEntryOptions options) =>
		new()
		{
			AbsoluteExpiration = options.AbsoluteExpiration,
			AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow,
			SlidingExpiration = options.SlidingExpiration
		};

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
