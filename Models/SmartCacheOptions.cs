namespace CleverCache.Models
{
    public class CleverCacheOptions(CleverCacheScanOptions? scanOptions = null,
        HashSet<DependentCache>? dependentCaches = null,
        bool disableAllScanning = false)
    {
        // ReSharper disable once IdentifierTypo
        public CleverCacheScanOptions Scanning { get; set; } = scanOptions ?? new CleverCacheScanOptions();
        public HashSet<DependentCache> DependentCaches { get; set; } = dependentCaches ?? [];
        public bool DisableAllScanning { get; set; } = disableAllScanning; // Don't do any scanning to set up 
    }
}