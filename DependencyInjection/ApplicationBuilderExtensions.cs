using Microsoft.AspNetCore.Builder;

namespace SmartCache.DependencyInjection
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseSmartCache<TContext>(this IApplicationBuilder app) where TContext : DbContext
        {
            var cache = app.ApplicationServices.GetRequiredService<ISmartCache>();
            var smartCacheOptions = app.ApplicationServices.GetRequiredService<SmartCacheOptions>();
            var dbContext = app.ApplicationServices.GetRequiredService<TContext>();
            var dependentCaches = smartCacheOptions.DependentCaches.ToList();

            dbContext.EnsureSmartCacheInterceptor();

            if (!smartCacheOptions.DisableAllScanning)
            {
                dependentCaches.AddRange(dbContext.DiscoverDependentCaches(smartCacheOptions));
            }

            foreach (var dependentCache in dependentCaches.Distinct())
            {
                cache.AddDependentCache(dependentCache.Type, dependentCache.DependentType);
            }

            return app;
        }
    }
}