using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using CleverCache.Attributes;
using CleverCache.Exceptions;
using CleverCache.Helpers;

namespace CleverCache.Extensions;

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

	public static List<DependentCache> DiscoverDependentCaches(this DbContext dbContext, CleverCacheOptions smartCacheOptions)
	{
		HashSet<DependentCache> dependentCaches = [];

		foreach (var entityType in dbContext.Model.GetEntityTypes())
		{
			if (smartCacheOptions.Scanning.NavigationScanMode != DependentCacheNavigationScanMode.None)
			{
				NavigationScanningHelper.Scan(smartCacheOptions.Scanning, entityType, dependentCaches);
			}

			// Its pointless doing attribute based processing if we already did recursive scanning
			if (smartCacheOptions.Scanning.NavigationScanMode != DependentCacheNavigationScanMode.Recursive)
			{
				ProcessAttribute(dbContext, entityType, dependentCaches);
			}
		}

		return [.. dependentCaches];
	}

	private static void ProcessAttribute(DbContext dbContext, IEntityType entityType, HashSet<DependentCache> dependentCaches)
	{
		// Check if this has attribute
		var type = entityType.ClrType;
		var attribute = type.GetCustomAttribute<DependantCachesAttribute>();
		if (attribute is null)
		{
			return;
		}

		// Do the mappings
		foreach (var dependentType in attribute.DependantTypes)
		{
			dependentCaches.Add(new DependentCache(type, dependentType));
			if (attribute.Reverse)
			{
				dependentCaches.Add(new DependentCache(dependentType, type)); ;
			}

			if (attribute.NavigationScanMode == DependentCacheNavigationScanMode.None)
			{
				continue;
			}

			var dependentModelType = dbContext.Model.FindEntityType(dependentType);

			if (dependentModelType is null)
			{
				continue;
			}

			NavigationScanningHelper.Scan(
				new CleverCacheScanOptions(attribute.NavigationScanMode, attribute.Reverse),
				entityType,
				dependentCaches);
		}
	}
}