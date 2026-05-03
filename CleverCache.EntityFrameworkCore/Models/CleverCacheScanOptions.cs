namespace CleverCache.EntityFrameworkCore.Models;

public class CleverCacheScanOptions(
	DependentCacheNavigationScanMode navigationScanMode = DependentCacheNavigationScanMode.Direct,
	bool reverseNavigationDependencies = false
)
{
	public DependentCacheNavigationScanMode NavigationScanMode { get; set; } = navigationScanMode;
	public bool ReverseNavigationDependencies { get; set; } = reverseNavigationDependencies;
}
