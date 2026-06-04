namespace CleverCache;
using System.Collections.Concurrent;

internal abstract class CacheEntryManager
{
	// type -> set of keys (thread-safe “set” via ConcurrentDictionary<TKey, byte>)
	private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, byte>> _keysByType = new();

	// type -> dependents (direct edges). We’ll expand transitively at runtime.
	private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Type, byte>> _dependants = new();

	/// <summary>
	/// Adds a dependent relationship between two types.
	/// </summary>
	public void AddDependentCache(Type type, Type dependentType)
	{
		var set = _dependants.GetOrAdd(type, _ => new ConcurrentDictionary<Type, byte>());
		set.TryAdd(dependentType, 0);
	}

	/// <summary>
	/// Associates key with each type and all of its transitive dependents.
	/// </summary>
	public void AddKeyToTypes(Type[] types, object key)
	{
		var canonicalKey = CacheKeyIdentity.ToCanonicalKey(key);
		AddCanonicalKeyToTypes(types, canonicalKey);
	}

	protected void AddCanonicalKeyToTypes(Type[] types, string canonicalKey)
	{
		var all = ExpandTransitively(types);
		foreach (var t in all)
		{
			_keysByType.GetOrAdd(t, _ => new ConcurrentDictionary<string, byte>()).TryAdd(canonicalKey, 0);
		}
	}

	/// <summary>
	/// Removes a key from every type's tracked key set.
	/// Called after a key is removed from the store so the tracking set stays in sync.
	/// </summary>
	protected void RemoveKeyFromAllTypes(object key)
	{
		var canonicalKey = key is string s && CacheKeyIdentity.IsCanonicalKey(s)
			? s
			: CacheKeyIdentity.ToCanonicalKey(key);

		foreach (var typeSet in _keysByType.Values)
			typeSet.TryRemove(canonicalKey, out _);
	}

	/// <summary>
	/// Snapshot keys for a given type (safe to enumerate).
	/// </summary>
	protected string[] SnapshotKeysFor(Type type) =>
		_keysByType.TryGetValue(type, out var set) ? set.Keys.ToArray() : [];

	/// <summary>
	/// Snapshot of the full dependency graph and tracked keys.
	/// </summary>
	protected CleverCacheDiagnostics SnapshotDiagnostics()
	{
		var dependants = _dependants.ToDictionary(
			kvp => kvp.Key,
			kvp => (IReadOnlyList<Type>)kvp.Value.Keys.OrderBy(t => t.Name).ToList());

		var keysByType = _keysByType.ToDictionary(
			kvp => kvp.Key,
			kvp => (IReadOnlyList<object>)kvp.Value.Keys.Cast<object>().ToList());

		return new CleverCacheDiagnostics(dependants, keysByType);
	}
	/// <summary>
	/// Transitive closure over dependents, cycle-safe
	/// </summary>
	private HashSet<Type> ExpandTransitively(IEnumerable<Type> roots)
	{
		var visited = new HashSet<Type>();
		var stack = new Stack<Type>(roots);

		while (stack.Count > 0)
		{
			var t = stack.Pop();
			if (!visited.Add(t)) continue;

			if (!_dependants.TryGetValue(t, out var dependants)) continue;
			foreach (var d in dependants.Keys)
				if (!visited.Contains(d)) stack.Push(d);
		}

		return visited;
	}
}
