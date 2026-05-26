using Microsoft.AspNetCore.Builder;

namespace CleverCache.EntityFrameworkCore.DependencyInjection;

public static class ApplicationBuilderExtensions
{
	/// <summary>
	/// Scans the <see cref="DbContext"/> navigation properties for <typeparamref name="TContext"/>
	/// and registers the discovered cache dependency rules at startup.
	/// Can be called multiple times for multiple <see cref="DbContext"/> types, each with their own scan options.
	/// </summary>
	/// <remarks>
	/// Use this overload in ASP.NET Core apps where <see cref="IApplicationBuilder"/> is available.
	/// For worker services or console apps, use the <see cref="IServiceProvider"/> overload instead.
	/// </remarks>
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
		app.ApplicationServices.ScanDbSetsForCacheDependencies<TContext>(configure);
		return app;
	}

	/// <summary>
	/// Scans the <see cref="DbContext"/> navigation properties for <typeparamref name="TContext"/>
	/// and registers the discovered cache dependency rules at startup.
	/// Can be called multiple times for multiple <see cref="DbContext"/> types, each with their own scan options.
	/// </summary>
	/// <remarks>
	/// Use this overload in worker services or console apps where <see cref="IApplicationBuilder"/> is not available.
	/// </remarks>
	/// <example>
	/// <code>
	/// host.Services.ScanDbSetsForCacheDependencies&lt;AppDbContext&gt;(o =>
	///     o.NavigationScanMode = DependentCacheNavigationScanMode.Direct);
	/// await host.RunAsync();
	/// </code>
	/// </example>
	public static IServiceProvider ScanDbSetsForCacheDependencies<TContext>(
		this IServiceProvider services,
		Action<CleverCacheScanOptions>? configure = null)
		where TContext : DbContext
	{
		var cache = services.GetRequiredService<ICleverCache>();
		var scanOptions = new CleverCacheScanOptions();
		configure?.Invoke(scanOptions);

		using var scope = services.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

		foreach (var dep in dbContext.DiscoverDependentCaches(scanOptions))
			cache.AddDependentCache(dep.Type, dep.DependentType);

		return services;
	}
}
