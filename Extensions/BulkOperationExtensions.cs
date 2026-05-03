namespace CleverCache.Extensions;

/// <summary>
/// Fluent cache invalidation helpers to use after EF Core bulk operations
/// (ExecuteDelete, ExecuteUpdate) which bypass the change tracker and therefore
/// do not trigger automatic CleverCache invalidation.
/// </summary>
public static class BulkOperationExtensions
{
	/// <summary>
	/// Invalidates the cache for the specified types and returns the row count.
	/// Use after ExecuteDelete / ExecuteUpdate to keep the cache consistent.
	/// </summary>
	public static int InvalidateCaches(this int rowCount, ICleverCache cache, params Type[] types)
	{
		foreach (var type in types)
			cache.RemoveByType(type);
		return rowCount;
	}

	/// <summary>
	/// Invalidates the cache for <typeparamref name="T"/> and returns the row count.
	/// </summary>
	public static int InvalidateCaches<T>(this int rowCount, ICleverCache cache)
		=> rowCount.InvalidateCaches(cache, typeof(T));

	/// <summary>
	/// Awaits the bulk operation task, invalidates the cache for the specified types, and returns the row count.
	/// Use after ExecuteDeleteAsync / ExecuteUpdateAsync to keep the cache consistent.
	/// </summary>
	public static async Task<int> InvalidateCaches(this Task<int> task, ICleverCache cache, params Type[] types)
	{
		var rowCount = await task.ConfigureAwait(false);
		foreach (var type in types)
			cache.RemoveByType(type);
		return rowCount;
	}

	/// <summary>
	/// Awaits the bulk operation task, invalidates the cache for <typeparamref name="T"/>, and returns the row count.
	/// </summary>
	public static Task<int> InvalidateCaches<T>(this Task<int> task, ICleverCache cache)
		=> task.InvalidateCaches(cache, typeof(T));
}
