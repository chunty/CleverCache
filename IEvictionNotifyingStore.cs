namespace CleverCache;

/// <summary>
/// Optional interface for cache stores that can notify when entries are evicted.
/// Implement this alongside <see cref="ICleverCacheStore"/> to enable automatic
/// cleanup of tracked keys when entries expire or are evicted by the store.
/// <para>
/// <see cref="Implementations.MemoryCacheStore"/> implements this interface.
/// <see cref="Implementations.DistributedCacheStore"/> does not, as distributed caches
/// have no eviction notification mechanism.
/// </para>
/// </summary>
public interface IEvictionNotifyingStore
{
	/// <summary>
	/// Registers a callback to be invoked when a cache entry is evicted.
	/// </summary>
	/// <param name="onEvicted">Called with the evicted key.</param>
	void RegisterEvictionCallback(Action<object> onEvicted);
}
