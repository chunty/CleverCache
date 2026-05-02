namespace CleverCache.Implementations;

/// <summary>
/// A fake implementation of the ICleverCache interface for testing purposes.
/// </summary>
public class FakeCache : ICleverCache
{
	/// <inheritdoc/>
	public void AddDependentCache(Type type, Type dependentType) { }

	/// <inheritdoc/>
	public void AddKeyToTypes(Type[] types, object key) { }

	/// <inheritdoc/>
	public void RemoveByType(Type type) { }

	/// <inheritdoc/>
	public TItem? GetOrCreate<TItem>(
		Type[] types,
		object key,
		Func<TItem> factory,
		CleverCacheEntryOptions? createOptions = null) => factory();

	/// <inheritdoc/>
	public async Task<TItem?> GetOrCreateAsync<TItem>(
		Type[] types,
		object key,
		Func<Task<TItem>> factory,
		CleverCacheEntryOptions? createOptions = null) => await factory();

	/// <inheritdoc/>
	public void Remove(object key) { }
}

