using Microsoft.AspNetCore.Builder;

namespace CleverCache.EntityFrameworkCore.DependencyInjection;

public static class ApplicationBuilderExtensions
{
	/// <summary>
	/// Scans the <see cref="DbContext"/> navigation properties for <typeparamref name="TContext"/>
	/// and registers the discovered cache dependency rules at startup.
	/// Can be called multiple times for multiple <see cref="DbContext"/> types, each with their own scan options.
	/// </summary>
	/// <example>
	/// <code>
	/// app.ScanDbSetsForCacheDependencies&lt;AppDbContext&gt;(o =>
	///     o.NavigationScanMode = DependentCacheNavigationScanMode.Direct);
	/// </code>
	/// </example>
	public static IApplicationBuilder ScanDbSetsForCacheDependencies<TContext>(
		this IApplicationBuilder app,
		Action<CleverCacheScanOptions>? configure = null)
		where TContext : DbContext
	{
		var cache = app.ApplicationServices.GetRequiredService<ICleverCache>();
		var scanOptions = new CleverCacheScanOptions();
		configure?.Invoke(scanOptions);

		using var scope = app.ApplicationServices.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

		foreach (var dep in dbContext.DiscoverDependentCaches(scanOptions))
			cache.AddDependentCache(dep.Type, dep.DependentType);

		return app;
	}
}
