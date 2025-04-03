namespace CleverCache.Models;

public class CleverCacheScanOptions(
	DependentCacheNavigationScanMode navigationScanMode = DependentCacheNavigationScanMode.None,
	bool reverseNavigationDependencies = false
)
{
	public DependentCacheNavigationScanMode NavigationScanMode { get; set; } = navigationScanMode;
	public bool ReverseNavigationDependencies { get; set; } = reverseNavigationDependencies;
}