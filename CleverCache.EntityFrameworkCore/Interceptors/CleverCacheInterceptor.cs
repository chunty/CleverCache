using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Collections.Concurrent;

namespace CleverCache.EntityFrameworkCore.Interceptors;

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

	public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
	{
		var savedChanges  = base.SavedChanges(eventData, result);
		InvalidateAndClear(eventData);
		return savedChanges;
	}

	public override ValueTask<int> SavedChangesAsync(
		SaveChangesCompletedEventData eventData,
		int result,
		CancellationToken cancellationToken = default)
	{
		return InvalidateAndClearAsync(eventData, result, cancellationToken);
	}

	private async ValueTask<int> InvalidateAndClearAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken)
	{
		var savedChanges = await base.SavedChangesAsync(eventData, result, cancellationToken);
		if (eventData.Context is null) return savedChanges;
		if (!_pendingTypes.TryRemove(eventData.Context.ContextId, out var types) || types is not { Count: > 0 }) return savedChanges;
		foreach (var t in types)
			await cache.RemoveByTypeAsync(t, cancellationToken).ConfigureAwait(false);
		return savedChanges;
	}

	public override void SaveChangesFailed(DbContextErrorEventData eventData)
	{
		if (eventData.Context != null)
			_pendingTypes.TryRemove(eventData.Context.ContextId, out _);
		base.SaveChangesFailed(eventData);
	}

	public override Task SaveChangesFailedAsync(
		DbContextErrorEventData eventData,
		CancellationToken cancellationToken = default)
	{
		if (eventData.Context != null)
			_pendingTypes.TryRemove(eventData.Context.ContextId, out _);
		return base.SaveChangesFailedAsync(eventData, cancellationToken);
	}

	private void CaptureTypes(DbContextEventData eventData)
	{
		if (eventData.Context is null) return;

		var contextId = eventData.Context.ContextId;
		var set = _pendingTypes.GetOrAdd(contextId, _ => []);
		foreach (var entry in eventData.Context.ChangeTracker.Entries())
		{
			if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
				set.Add(entry.Metadata.ClrType);
		}
	}

	private void InvalidateAndClear(SaveChangesCompletedEventData eventData)
	{
		if (eventData.Context is null) return;
		if (!_pendingTypes.TryRemove(eventData.Context.ContextId, out var types) || types is not { Count: > 0 }) return;
		foreach (var t in types.Distinct())
			cache.RemoveByType(t);
	}
}
