using Microsoft.EntityFrameworkCore.Metadata;

namespace SmartCache.Helpers
{
    internal static class NavigationScanningHelper
    {
        public static void Scan(SmartCacheScanOptions scanOptions, IEntityType entityType, HashSet<DependentCache> dependentCaches)
        {
            foreach (var navigation in entityType.GetNavigations())
            {
                var sourceType = entityType.ClrType;
                var dependentEntityType = navigation.TargetEntityType;
                var dependentType = dependentEntityType.ClrType;

                // If we already have this in the list, skip
                if (dependentCaches.Any(x => x.Type == dependentType))
                {
                    continue;
                }

                dependentCaches.Add(new DependentCache(sourceType, dependentType));
                if (scanOptions.ReverseNavigationDependencies)
                {
                    dependentCaches.Add(new DependentCache(dependentType, sourceType));
                }

                if (!scanOptions.NavigationScanMode.Equals(DependentCacheNavigationScanMode.Recursive))
                {
                    continue;
                }

                Scan(scanOptions, dependentEntityType, dependentCaches);
            }
        }
    }
}
