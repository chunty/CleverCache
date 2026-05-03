using Microsoft.EntityFrameworkCore.Metadata;

namespace CleverCache.EntityFrameworkCore.Helpers;

internal static class NavigationScanningHelper
{
	public static void Scan(CleverCacheScanOptions scanOptions, IEntityType entityType, HashSet<DependentCache> dependentCaches)
	{
		foreach (var navigation in entityType.GetNavigations())
		{
			var sourceType = entityType.ClrType;
			var dependentEntityType = navigation.TargetEntityType;
			var dependentType = dependentEntityType.ClrType;

			if (dependentCaches.Any(x => x.Type == dependentType))
				continue;

			dependentCaches.Add(new DependentCache(sourceType, dependentType));
			if (scanOptions.ReverseNavigationDependencies)
				dependentCaches.Add(new DependentCache(dependentType, sourceType));

			if (scanOptions.NavigationScanMode == DependentCacheNavigationScanMode.Recursive)
				Scan(scanOptions, dependentEntityType, dependentCaches);
		}
	}
}
