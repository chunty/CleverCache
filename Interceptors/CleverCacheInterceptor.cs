using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Collections.Concurrent;

namespace CleverCache.Interceptors;

/// <summary>
/// Interceptor to clear smart memory cache after changes are saved to the database.
/// </summary>
public class CleverCacheInterceptor(ICleverCache cache) : SaveChangesInterceptor
{
	// Keep pending types per DbContext to avoid cross-talk and races.
	private readonly ConcurrentDictionary<DbContextId, HashSet<Type>> _pendingTypes = new();

	public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
	{
		CaptureTypes(eventData);
		return result;
	}

	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureTypes(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }


	/// <summary>
	/// Synchronously handles the event after changes are saved to the database.
	/// </summary>
	/// <param name="eventData">The event data.</param>
	/// <param name="result">The result of the save operation.</param>
	/// <returns>The result of the save operation.</returns>
	public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
	{
		InvalidateAndClear(eventData);
		return base.SavedChanges(eventData, result);
	}

	/// <summary>
	/// Asynchronously handles the event after changes are saved to the database.
	/// </summary>
	/// <param name="eventData">The event data.</param>
	/// <param name="result">The result of the save operation.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the save operation.</returns>
	public override ValueTask<int> SavedChangesAsync(
		SaveChangesCompletedEventData eventData,
		int result,
		CancellationToken cancellationToken = default)
	{
		InvalidateAndClear(eventData);
		return base.SavedChangesAsync(eventData, result, cancellationToken);
	}

	public override void SaveChangesFailed(DbContextErrorEventData eventData)
	{
		// Guard for null Context (defensive; eventData.Context is nullable)
		if (eventData.Context != null)
		{
			_pendingTypes.TryRemove(eventData.Context.ContextId, out _);
		}
		base.SaveChangesFailed(eventData);
	}

	public override Task SaveChangesFailedAsync(
		DbContextErrorEventData eventData,
		CancellationToken cancellationToken = default)
	{
		if (eventData.Context != null)
		{
			_pendingTypes.TryRemove(eventData.Context.ContextId, out _);
		}
		return base.SaveChangesFailedAsync(eventData, cancellationToken);
	}

	private void CaptureTypes(DbContextEventData eventData)
	{
		if (eventData.Context is null) return;

		var contextId = eventData.Context.ContextId;

		var set = _pendingTypes.GetOrAdd(contextId, _ => new HashSet<Type>());
		foreach (var entry in eventData.Context.ChangeTracker.Entries())
		{
			if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
			{
				set.Add(entry.Metadata.ClrType);
			}
		}
	}

	private void InvalidateAndClear(SaveChangesCompletedEventData eventData)
	{
		if (eventData.Context is null) return;

		if (_pendingTypes.TryRemove(eventData.Context.ContextId, out var types) && types is { Count: > 0 })
		{
			foreach (var t in types)
			{
				cache.RemoveByType(t);
			}
		}
	}
}