using Microsoft.AspNetCore.Builder;

namespace CleverCache.DependencyInjection;

public static class ApplicationBuilderExtensions
{
	public static IApplicationBuilder UseCleverCache<TContext>(this IApplicationBuilder app) where TContext : DbContext
	{
		var cache = app.ApplicationServices.GetRequiredService<ICleverCache>();
		var smartCacheOptions = app.ApplicationServices.GetRequiredService<CleverCacheOptions>();

		using var scope = app.ApplicationServices.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
		var dependentCaches = smartCacheOptions.DependentCaches.ToList();

		dbContext.EnsureCleverCacheInterceptor();

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