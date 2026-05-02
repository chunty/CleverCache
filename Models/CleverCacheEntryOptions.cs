namespace CleverCache.Models;

public class CleverCacheEntryOptions
{
	public DateTimeOffset? AbsoluteExpiration { get; set; }
	public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
	public TimeSpan? SlidingExpiration { get; set; }
}
