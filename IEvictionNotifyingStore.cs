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
	/// <remarks>
	/// Notifications may be delayed or delivered synchronously. CleverCache checks whether the key
	/// still exists before removing its tracking, so eviction of an old entry cannot untrack a replacement.
	/// Notifying stores must support <c>TryGet&lt;object&gt;</c> for this existence check, including cached nulls.
	/// </remarks>
	/// <param name="onEvicted">Called with the evicted key.</param>
	void RegisterEvictionCallback(Action<object> onEvicted);
}
