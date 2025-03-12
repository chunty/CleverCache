using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CleverCache.Interceptors
{
    /// <summary>
    /// Interceptor to clear smart memory cache after changes are saved to the database.
    /// </summary>
    public class CleverCacheInterceptor(ICleverCache cache) : SaveChangesInterceptor
    {
        /// <summary>
        /// Synchronously handles the event after changes are saved to the database.
        /// </summary>
        /// <param name="eventData">The event data.</param>
        /// <param name="result">The result of the save operation.</param>
        /// <returns>The result of the save operation.</returns>
        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            // Call the asynchronous method and wait for its completion.
            return SavedChangesAsync(eventData, result).AsTask().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously handles the event after changes are saved to the database.
        /// </summary>
        /// <param name="eventData">The event data.</param>
        /// <param name="result">The result of the save operation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the save operation.</returns>
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            // If the context is null, call the base method.
            if (eventData.Context is null)
            {
                return await base.SavedChangesAsync(eventData, result, cancellationToken);
            }

            // Get the distinct types of the entities that have changed.
            var types = eventData.Context.ChangeTracker
                .Entries()
                .Select(x => x.Entity.GetType())
                .Distinct();

            // Remove cache entries for each type.
            foreach (var type in types)
            {
                cache.RemoveTypeKeys(type);
            }

            // Call the base method.
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }
    }
}
