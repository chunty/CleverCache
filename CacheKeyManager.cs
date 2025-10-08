namespace CleverCache;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

public abstract class CacheEntryManager : ICacheEntryManager
{
	// type -> set of keys (thread-safe “set” via ConcurrentDictionary<TKey, byte>)
	private readonly ConcurrentDictionary<Type, ConcurrentDictionary<object, byte>> _keysByType = new();

	// type -> dependents (direct edges). We’ll expand transitively at runtime.
	private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Type, byte>> _dependants = new();

	// === Public API (same intent as your original) ===

	public void AddDependentCache(Type type, Type dependentType)
	{
		var set = _dependants.GetOrAdd(type, _ => new ConcurrentDictionary<Type, byte>());
		set.TryAdd(dependentType, 0);
	}

	/// Associates key with each type and all of its transitive dependents.
	public void AddKeyToTypes(Type[] types, object key)
	{
		var all = ExpandTransitively(types);
		foreach (var t in all)
			_keysByType.GetOrAdd(t, _ => new ConcurrentDictionary<object, byte>()).TryAdd(key, 0);
	}

	// === Protected helpers for derived cache implementation ===

	/// Call this when you create an IMemoryCache entry so we auto-untrack on eviction.
	protected void TrackWithEviction(ICacheEntry entry, Type[] types, object key)
	{
		// Record now …
		AddKeyToTypes(types, key);
		
		// …and clean up when the entry goes away.
		var all = ExpandTransitively(types);
		entry.RegisterPostEvictionCallback((k, _, __, ___) =>
		{
			foreach (var t in all)
				if (_keysByType.TryGetValue(t, out var set))
					set.TryRemove(k, out var _);
		});
	}

	/// Snapshot keys for a given type (safe to enumerate).
	protected object[] SnapshotKeysFor(Type type) =>
		_keysByType.TryGetValue(type, out var set) ? set.Keys.ToArray() : [];

	/// Optional tidy-up if you manually remove entries (eviction callback also cleans).
	protected void UntrackKeyFor(Type type, object key)
	{
		if (_keysByType.TryGetValue(type, out var set))
			set.TryRemove(key, out _);
	}

	// === Private: transitive closure over dependents, cycle-safe ===

	private HashSet<Type> ExpandTransitively(IEnumerable<Type> roots)
	{
		var visited = new HashSet<Type>();
		var stack = new Stack<Type>(roots);

		while (stack.Count > 0)
		{
			var t = stack.Pop();
			if (!visited.Add(t)) continue;

			if (_dependants.TryGetValue(t, out var deps))
				foreach (var d in deps.Keys)
					if (!visited.Contains(d)) stack.Push(d);
		}

		return visited;
	}
}

