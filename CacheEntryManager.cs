namespace CleverCache;
public abstract class CacheEntryManager : ICleverCacheEntryManager
{
	protected readonly HashSet<CacheTypeMap> CacheEntries = [];
	protected readonly HashSet<DependentCache> DependentCaches = [];

	/* Public methods */

	/// <inheritdoc />
	public void AddDependentCache(Type type, Type dependentType) =>
		DependentCaches.Add(new DependentCache(type, dependentType));

	/// <inheritdoc />
	public void AddKeyToTypes(Type[] types, object key)
	{
		foreach (var type in types)
		{
			CacheEntries.Add(new CacheTypeMap(type, key));
			foreach (var dependentCache in DependentCaches.Where(x => x.Type == type))
			{
				this.AddKeyToType(dependentCache.DependentType, key);
			}
		}
	}
}