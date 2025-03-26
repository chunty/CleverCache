using Microsoft.Extensions.Primitives;

namespace CleverCache.Models;

public class FakeCacheEntry : ICacheEntry
{
	public void Dispose()
	{
		GC.SuppressFinalize(this);
	}

	public object Key { get; } = Guid.NewGuid();
	public object? Value { get; set; }
	public DateTimeOffset? AbsoluteExpiration { get; set; }
	public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
	public TimeSpan? SlidingExpiration { get; set; }
	public IList<IChangeToken> ExpirationTokens { get; } = [];
	public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; } = [];
	public CacheItemPriority Priority { get; set; }
	public long? Size { get; set; }
}