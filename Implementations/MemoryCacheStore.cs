using Microsoft.Extensions.Caching.Memory;

namespace CleverCache.Implementations;

public class MemoryCacheStore(IMemoryCache memoryCache) : ICleverCacheStore
{
	public bool TryGet<TItem>(object key, out TItem? value)
	{
		if (memoryCache.TryGetValue(key, out var hit))
		{
			value = (TItem?)hit;
			return true;
		}

		value = default;
		return false;
	}

	public Task<(bool Hit, TItem? Value)> TryGetAsync<TItem>(object key, CancellationToken cancellationToken = default)
	{
		var hit = TryGet<TItem>(key, out var value);
		return Task.FromResult((hit, value));
	}

	public void Set<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null)
	{
		using var entry = memoryCache.CreateEntry(key);
		entry.Value = value;

		if (options is null) return;
		entry.AbsoluteExpiration = options.AbsoluteExpiration;
		entry.AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow;
		entry.SlidingExpiration = options.SlidingExpiration;
	}

	public Task SetAsync<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
	{
		Set(key, value, options);
		return Task.CompletedTask;
	}

	public void Remove(object key) => memoryCache.Remove(key);

	public Task RemoveAsync(object key, CancellationToken cancellationToken = default)
	{
		Remove(key);
		return Task.CompletedTask;
	}
}
