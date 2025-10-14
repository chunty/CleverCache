namespace CleverCache;
using System.Collections.Concurrent;

public abstract class CacheEntryManager: ICacheEntryManager
{
	// type -> set of keys (thread-safe “set” via ConcurrentDictionary<TKey, byte>)
	private readonly ConcurrentDictionary<Type, ConcurrentDictionary<object, byte>> _keysByType = new();

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
		var all = ExpandTransitively(types);
		foreach (var t in all)
		{
			_keysByType.GetOrAdd(t, _ => new ConcurrentDictionary<object, byte>()).TryAdd(key, 0);
		}
			
	}

	/// <summary>
	/// Snapshot keys for a given type (safe to enumerate).
	/// </summary>
	protected object[] SnapshotKeysFor(Type type) =>
		_keysByType.TryGetValue(type, out var set) ? set.Keys.ToArray() : [];

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

