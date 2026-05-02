using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace CleverCache.Implementations;

public class DistributedCacheStore(IDistributedCache distributedCache) : ICleverCacheStore
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public bool TryGet<TItem>(object key, out TItem? value)
	{
		var bytes = distributedCache.Get(ToStringKey<TItem>(key));
		if (bytes is null)
		{
			value = default;
			return false;
		}

		value = JsonSerializer.Deserialize<TItem>(bytes, JsonOptions);
		return true;
	}

	public async Task<(bool Hit, TItem? Value)> TryGetAsync<TItem>(object key, CancellationToken cancellationToken = default)
	{
		var bytes = await distributedCache.GetAsync(ToStringKey<TItem>(key), cancellationToken).ConfigureAwait(false);
		if (bytes is null) return (false, default);

		var value = JsonSerializer.Deserialize<TItem>(bytes, JsonOptions);
		return (true, value);
	}

	public void Set<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null)
	{
		var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
		distributedCache.Set(ToStringKey<TItem>(key), bytes, ToDistributedOptions(options));
	}

	public async Task SetAsync<TItem>(object key, TItem value, CleverCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
	{
		var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
		await distributedCache.SetAsync(ToStringKey<TItem>(key), bytes, ToDistributedOptions(options), cancellationToken).ConfigureAwait(false);
	}

	public void Remove(object key) => distributedCache.Remove(ToStringKey<object>(key));

	public async Task RemoveAsync(object key, CancellationToken cancellationToken = default) =>
		await distributedCache.RemoveAsync(ToStringKey<object>(key), cancellationToken).ConfigureAwait(false);

	private static string ToStringKey<TItem>(object key) =>
		$"{typeof(TItem).FullName}:{JsonSerializer.Serialize(key, JsonOptions)}";

	private static DistributedCacheEntryOptions ToDistributedOptions(CleverCacheEntryOptions? options) =>
		options is null
			? new DistributedCacheEntryOptions()
			: new DistributedCacheEntryOptions
			{
				AbsoluteExpiration = options.AbsoluteExpiration,
				AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow,
				SlidingExpiration = options.SlidingExpiration
			};
}
