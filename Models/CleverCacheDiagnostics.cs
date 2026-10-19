namespace CleverCache.Models;

/// <summary>
/// A point-in-time snapshot of the cache's dependency graph and tracked keys.
/// </summary>
/// <param name="Dependants">Direct dependency edges: each key type's cached data depends on its listed types, so invalidating a listed type also invalidates caches for the key type.</param>
/// <param name="KeysByType">Cache keys currently tracked per type (including keys inherited via cascade expansion).</param>
public record CleverCacheDiagnostics(
	IReadOnlyDictionary<Type, IReadOnlyList<Type>> Dependants,
	IReadOnlyDictionary<Type, IReadOnlyList<object>> KeysByType
);
