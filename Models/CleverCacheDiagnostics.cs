namespace CleverCache.Models;

/// <summary>
/// A point-in-time snapshot of the cache's dependency graph and tracked keys.
/// </summary>
/// <param name="Dependants">Direct cascade edges: when a type is invalidated, all listed types are also invalidated.</param>
/// <param name="KeysByType">Cache keys currently tracked per type (including keys inherited via cascade expansion).</param>
public record CleverCacheDiagnostics(
	IReadOnlyDictionary<Type, IReadOnlyList<Type>> Dependants,
	IReadOnlyDictionary<Type, IReadOnlyList<object>> KeysByType
);
