using System.Reflection;
using CleverCache.Attributes;
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
		HashSet<DependentCache> dependentCaches = [];

		foreach (var entityType in dbContext.Model.GetEntityTypes())
		{
			if (scanOptions.NavigationScanMode != DependentCacheNavigationScanMode.None)
				NavigationScanningHelper.Scan(scanOptions, entityType, dependentCaches);

			// Attribute processing is redundant if recursive scanning already covered the full graph
			if (scanOptions.NavigationScanMode != DependentCacheNavigationScanMode.Recursive)
				ProcessAttribute(dbContext, entityType, dependentCaches);
		}

		return [.. dependentCaches];
	}

	private static void ProcessAttribute(DbContext dbContext, IEntityType entityType, HashSet<DependentCache> dependentCaches)
	{
		var type = entityType.ClrType;
		var attribute = type.GetCustomAttribute<DependentCachesAttribute>();
		if (attribute is null) return;

		foreach (var dependentType in attribute.DependantTypes)
		{
			dependentCaches.Add(new DependentCache(type, dependentType));
			if (attribute.Reverse)
				dependentCaches.Add(new DependentCache(dependentType, type));

			if (attribute.NavigationScanMode == DependentCacheNavigationScanMode.None)
				continue;

			var dependentModelType = dbContext.Model.FindEntityType(dependentType);
			if (dependentModelType is null) continue;

			NavigationScanningHelper.Scan(
				new CleverCacheScanOptions(attribute.NavigationScanMode, attribute.Reverse),
				entityType,
				dependentCaches);
		}
	}
}
