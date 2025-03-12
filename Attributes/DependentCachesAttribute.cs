namespace SmartCache.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DependantCachesAttribute(
        Type[] types,
        DependentCacheNavigationScanMode navigationScanMode = DependentCacheNavigationScanMode.None,
        bool reverse = false
    ) : Attribute
    {
        public Type[] DependantTypes { get; } = types;
        public DependentCacheNavigationScanMode NavigationScanMode { get; set; } = navigationScanMode;
        public bool Reverse { get; set; } = reverse;
    }
}