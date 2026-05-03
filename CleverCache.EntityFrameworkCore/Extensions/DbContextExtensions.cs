using CleverCache.EntityFrameworkCore.Exceptions;
using CleverCache.EntityFrameworkCore.Helpers;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CleverCache.EntityFrameworkCore.Extensions;

internal static class DbContextExtensions
{
	public static void EnsureCleverCacheInterceptor(this DbContext dbContext)
	{
		var isRegistered = dbContext.GetService<IDbContextOptions>().Extensions
			.OfType<CoreOptionsExtension>()
			.Any(e => e.Interceptors != null
			          && e.Interceptors.Any(i => i.GetType() == typeof(CleverCacheInterceptor)));

		if (!isRegistered) throw new MissingInterceptorException();
	}

	public static List<DependentCache> DiscoverDependentCaches(this DbContext dbContext, CleverCacheScanOptions scanOptions)
	{
		if (scanOptions.NavigationScanMode == DependentCacheNavigationScanMode.None)
			return [];

		HashSet<DependentCache> dependentCaches = [];

		foreach (var entityType in dbContext.Model.GetEntityTypes())
			NavigationScanningHelper.Scan(scanOptions, entityType, dependentCaches);

		return [.. dependentCaches];
	}
}
