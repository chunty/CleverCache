namespace CleverCache;

public interface ICleverCache
{
	/// <summary>
	/// Registers a cascade rule: when all entries of <paramref name="type"/> are invalidated,
	/// all entries of <paramref name="dependentType"/> are also invalidated.
	/// Cascades are transitive and cycle-safe.
	/// </summary>
	/// <remarks>
	/// This is normally configured automatically via the <c>[DependentCaches]</c> attribute and
	/// <c>app.UseCleverCache&lt;TContext&gt;()</c>. Call this directly only when you need to set
	/// up cache dependencies programmatically rather than via the attribute.
	/// </remarks>
	void AddDependentCache(Type type, Type dependentType);

	/// <summary>
	/// Registers a cache key under the specified entity types so that the key is automatically
	/// evicted when data of any of those types changes (via <see cref="RemoveByType"/>).
	/// </summary>
	/// <remarks>
	/// This is called automatically by <see cref="GetOrCreate{TItem}"/> and
	/// <see cref="GetOrCreateAsync{TItem}"/>. Call this directly only when you need to associate
	/// a key with a type after the fact — for example, following a bulk operation that bypasses
	/// the EF Core change tracker.
	/// </remarks>
	void AddKeyToTypes(Type[] types, object key);

	/// <summary>
	/// Gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
	/// The entry is automatically evicted when data of any type in <paramref name="types"/> changes.
	/// </summary>
	/// <param name="types">The entity types this cache entry is associated with. The entry is evicted when any of these types is invalidated.</param>
	/// <param name="key">The key of the entry to look for or create.</param>
	/// <param name="factory">The factory that creates the value associated with this key if the key does not exist in the cache.</param>
	/// <param name="createOptions">The options to be applied to the cache entry if the key does not exist in the cache.</param>
	/// <returns>The value associated with this key.</returns>
	TItem? GetOrCreate<TItem>(
		Type[] types,
		object key,
		Func<TItem> factory,
		CleverCacheEntryOptions? createOptions = null);

	/// <summary>
	/// Asynchronously gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
	/// The entry is automatically evicted when data of any type in <paramref name="types"/> changes.
	/// </summary>
	/// <typeparam name="TItem">The type of the object to get.</typeparam>
	/// <param name="types">The entity types this cache entry is associated with. The entry is evicted when any of these types is invalidated.</param>
	/// <param name="key">The key of the entry to look for or create.</param>
	/// <param name="factory">The factory task that creates the value associated with this key if the key does not exist in the cache.</param>
	/// <param name="createOptions">The options to be applied to the cache entry if the key does not exist in the cache.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	Task<TItem?> GetOrCreateAsync<TItem>(
		Type[] types,
		object key,
		Func<Task<TItem>> factory,
		CleverCacheEntryOptions? createOptions = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Removes the cache entry with the specified key.
	/// </summary>
	/// <param name="key">The key identifying the entry to remove.</param>
	void Remove(object key);

	/// <summary>
	/// Returns a point-in-time snapshot of the dependency graph (cascade rules) and all
	/// currently tracked cache keys, grouped by entity type. Useful for diagnostics and debugging.
	/// </summary>
	CleverCacheDiagnostics GetDiagnostics();

	/// <summary>
	/// Removes all cache entries associated with the specified entity type, including entries
	/// registered under any dependent types (see <see cref="AddDependentCache"/>).
	/// </summary>
	/// <param name="type">The entity type whose cache entries should be evicted.</param>
	void RemoveByType(Type type);

	/// <summary>
	/// Asynchronously removes all cache entries associated with the specified entity type,
	/// including entries registered under any dependent types (see <see cref="AddDependentCache"/>).
	/// Prefer this over <see cref="RemoveByType"/> when using a distributed cache backend.
	/// </summary>
	/// <param name="type">The entity type whose cache entries should be evicted.</param>
	/// <param name="cancellationToken">A token to cancel the async operation.</param>
	Task RemoveByTypeAsync(Type type, CancellationToken cancellationToken = default);

}