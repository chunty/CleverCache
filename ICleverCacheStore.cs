namespace CleverCache;

/// <summary>
/// Abstraction over the underlying cache backend. Implement this interface to plug in a custom cache provider.
/// </summary>
public interface ICleverCacheStore
{
	/// <summary>Attempts to retrieve a cached value by key.</summary>
	bool TryGet<TItem>(object key, out TItem? value);

	/// <summary>Asynchronously attempts to retrieve a cached value by key.</summary>
	Task<(bool Hit, TItem? Value)> TryGetAsync<TItem>(object key, CancellationToken cancellationToken = default);

	/// <summary>Stores a value in the cache.</summary>
	void Set<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null);

	/// <summary>Asynchronously stores a value in the cache.</summary>
	Task SetAsync<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default);

	/// <summary>Removes a cached entry by key.</summary>
	void Remove(object key);

	/// <summary>Asynchronously removes a cached entry by key.</summary>
	Task RemoveAsync(object key, CancellationToken cancellationToken = default);
}
