namespace CleverCache;

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
	public Task RemoveByTypeAsync(Type type, CancellationToken cancellationToken = default) => Task.CompletedTask;

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
		CleverCacheEntryOptions? createOptions = null,
		CancellationToken cancellationToken = default) => await factory();

	/// <inheritdoc/>
	public void Remove(object key) { }

	/// <inheritdoc/>
	public CleverCacheDiagnostics GetDiagnostics() =>
		new(new Dictionary<Type, IReadOnlyList<Type>>(), new Dictionary<Type, IReadOnlyList<object>>());
}

