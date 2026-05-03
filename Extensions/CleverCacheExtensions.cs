namespace CleverCache.Extensions;
/// <summary>
/// Provides extension methods for the <see cref="ICleverCache"/> interface.
/// </summary>
public static class CleverCacheExtensions
{
	/// <summary>
	/// Gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
	/// </summary>
	/// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
	/// <typeparam name="TItem">The type of the object to get.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="key">The key of the entry to look for or create.</param>
	/// <param name="factory">The factory that creates the value associated with this key if the key does not exist in the cache.</param>
	/// <param name="createOptions">The options to be applied to the cache entry if the key does not exist in the cache.</param>
	/// <returns>The value associated with this key.</returns>
	public static TItem? GetOrCreate<T, TItem>(this ICleverCache cache, object key,
		Func<TItem> factory,
		CleverCacheEntryOptions? createOptions = null) where T : class =>
		cache.GetOrCreate(typeof(T), key, factory, createOptions);

	/// <summary>
	/// Gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
	/// </summary>
	/// <typeparam name="TItem">The type of the object to get.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="type">The type of the object the cache key belongs to.</param>
	/// <param name="key">The key of the entry to look for or create.</param>
	/// <param name="factory">The factory that creates the value associated with this key if the key does not exist in the cache.</param>
	/// <param name="createOptions">The options to be applied to the cache entry if the key does not exist in the cache.</param>
	/// <returns>The value associated with this key.</returns>
	public static TItem? GetOrCreate<TItem>(this ICleverCache cache, Type type, object key, Func<TItem> factory,
		CleverCacheEntryOptions? createOptions = null) =>
		cache.GetOrCreate([type], key, factory, createOptions);

	/// <summary>
	/// Asynchronously gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
	/// </summary>
	/// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
	/// <typeparam name="TItem">The type of the object to get.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="key">The key of the entry to look for or create.</param>
	/// <param name="factory">The factory task that creates the value associated with this key if the key does not exist in the cache.</param>
	/// <param name="createOptions">The options to be applied to the cache entry if the key does not exist in the cache.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public static async Task<TItem?> GetOrCreateAsync<T, TItem>(this ICleverCache cache, object key,
		Func<Task<TItem>> factory,
		CleverCacheEntryOptions? createOptions = null,
		CancellationToken cancellationToken = default) where T : class =>
		await cache.GetOrCreateAsync(typeof(T), key, factory, createOptions, cancellationToken);

	/// <summary>
	/// Asynchronously gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
	/// </summary>
	/// <typeparam name="TItem">The type of the object to get.</typeparam>
	/// <param name="cache">The cache instance.</param>
	/// <param name="type">The type of the object the cache key belongs to.</param>
	/// <param name="key">The key of the entry to look for or create.</param>
	/// <param name="factory">The factory task that creates the value associated with this key if the key does not exist in the cache.</param>
	/// <param name="createOptions">The options to be applied to the cache entry if the key does not exist in the cache.</param>
	/// <param name="cancellationToken">A token to cancel the async operation.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public static async Task<TItem?> GetOrCreateAsync<TItem>(this ICleverCache cache, Type type,
		object key,
		Func<Task<TItem>> factory,
		CleverCacheEntryOptions? createOptions = null,
		CancellationToken cancellationToken = default) =>
		await cache.GetOrCreateAsync([type], key, factory, createOptions, cancellationToken);

	/// <summary>
	/// Returns a human-readable representation of the dependency graph and tracked cache keys.
	/// Useful for debugging — log it at startup or on-demand via a diagnostic endpoint.
	/// </summary>
	/// <example>
	/// <code>
	/// logger.LogDebug(cache.RenderDependencyTree());
	/// </code>
	/// </example>
	public static string RenderDependencyTree(this ICleverCache cache)
	{
		var d = cache.GetDiagnostics();

		// Collect all types that appear in either cascades or key tracking
		var allTypes = new HashSet<Type>(d.Dependants.Keys);
		foreach (var cascades in d.Dependants.Values)
			foreach (var t in cascades) allTypes.Add(t);
		foreach (var t in d.KeysByType.Keys) allTypes.Add(t);

		if (allTypes.Count == 0)
			return "CleverCache: no types registered.";

		var sb = new System.Text.StringBuilder();
		sb.AppendLine("CleverCache Dependency Tree");
		sb.AppendLine(new string('─', 40));

		foreach (var type in allTypes.OrderBy(t => t.Name))
		{
			sb.AppendLine(type.Name);

			if (d.Dependants.TryGetValue(type, out var cascades) && cascades.Count > 0)
				sb.AppendLine($"  ↳ cascades to : {string.Join(", ", cascades.Select(t => t.Name))}");

			if (d.KeysByType.TryGetValue(type, out var keys) && keys.Count > 0)
				sb.AppendLine($"  ↳ tracked keys : {string.Join(", ", keys)}");
		}

		var totalKeys = d.KeysByType.Values.SelectMany(k => k).Distinct().Count();
		sb.Append($"─ {allTypes.Count} type(s) | {d.Dependants.Count} cascade rule(s) | {totalKeys} tracked key(s) ─");
		return sb.ToString();
	}
}

