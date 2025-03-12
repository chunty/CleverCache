namespace SmartCache.Models
{
    public class SmartCacheScanOptions(
        DependentCacheNavigationScanMode navigationScanMode = DependentCacheNavigationScanMode.None,
        bool reverseNavigationDependencies = false
    )
    {
        public DependentCacheNavigationScanMode NavigationScanMode { get; set; } = navigationScanMode;
        public bool ReverseNavigationDependencies { get; set; } = reverseNavigationDependencies;
    }
}