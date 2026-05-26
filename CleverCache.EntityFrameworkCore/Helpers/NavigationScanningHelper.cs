using Microsoft.EntityFrameworkCore.Metadata;

namespace CleverCache.EntityFrameworkCore.Helpers;

internal static class NavigationScanningHelper
{
	public static void Scan(CleverCacheScanOptions scanOptions, IEntityType entityType, HashSet<DependentCache> dependentCaches, HashSet<Type>? visited = null)
	{
		if (scanOptions.NavigationScanMode == DependentCacheNavigationScanMode.Recursive)
		{
			visited ??= [];
			if (!visited.Add(entityType.ClrType))
				return;
		}

		foreach (var navigation in entityType.GetNavigations())
		{
			var sourceType = entityType.ClrType;
			var dependentEntityType = navigation.TargetEntityType;
			var dependentType = dependentEntityType.ClrType;

			dependentCaches.Add(new DependentCache(sourceType, dependentType));
			if (scanOptions.ReverseNavigationDependencies)
				dependentCaches.Add(new DependentCache(dependentType, sourceType));

			if (scanOptions.NavigationScanMode == DependentCacheNavigationScanMode.Recursive)
				Scan(scanOptions, dependentEntityType, dependentCaches, visited);
		}
	}
}
