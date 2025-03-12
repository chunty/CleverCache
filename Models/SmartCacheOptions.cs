namespace SmartCache.Models
{
    public class SmartCacheOptions(SmartCacheScanOptions? scanOptions = null,
        HashSet<DependentCache>? dependentCaches = null,
        bool disableAllScanning = false)
    {
        // ReSharper disable once IdentifierTypo
        public SmartCacheScanOptions Scanning { get; set; } = scanOptions ?? new SmartCacheScanOptions();
        public HashSet<DependentCache> DependentCaches { get; set; } = dependentCaches ?? [];
        public bool DisableAllScanning { get; set; } = disableAllScanning; // Don't do any scanning to set up 
    }
}