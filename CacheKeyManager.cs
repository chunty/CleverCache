namespace CleverCache;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

internal abstract class CacheEntryManager
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
	/// Removes a key from every type's tracked key set.
	/// Called after a key is removed from the store so the tracking set stays in sync.
	/// </summary>
	protected void RemoveKeyFromAllTypes(object key)
	{
		foreach (var typeSet in _keysByType.Values)
			typeSet.TryRemove(key, out _);
	}

	/// <summary>
	/// Snapshot keys for a given type (safe to enumerate).
	/// </summary>
	protected object[] SnapshotKeysFor(Type type) =>
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
			kvp => (IReadOnlyList<object>)kvp.Value.Keys.Select(SerializeDiagnosticKey).ToList());

		return new CleverCacheDiagnostics(dependants, keysByType);
	}

	private static object SerializeDiagnosticKey(object key) => key switch
	{
		string s => s,
		ValueType v => v,
		Type t => t.FullName ?? t.Name,
		Delegate d => d.ToString() ?? d.GetType().FullName ?? d.GetType().Name,
		_ => FormatObjectDiagnosticValue(key, depth: 0)
	};

	private static string FormatObjectDiagnosticValue(object value, int depth)
	{
		if (depth >= 2)
			return value.ToString() ?? value.GetType().Name;

		var type = value.GetType();
		var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
			.Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
			.ToArray();

		if (properties.Length == 0)
			return value.ToString() ?? type.Name;

		var formattedProperties = properties
			.Select(p => $"{p.Name} = {FormatDiagnosticValue(p.GetValue(value), depth + 1)}");

		return $"{type.Name} {{ {string.Join(", ", formattedProperties)} }}";
	}

	private static string FormatDiagnosticValue(object? value, int depth)
	{
		if (value is null) return "null";
		if (value is string s) return $"\"{s}\"";
		if (value is Type t) return t.FullName ?? t.Name;
		if (value is ValueType v) return v.ToString() ?? v.GetType().Name;

		if (value is IEnumerable enumerable and not string)
			return FormatEnumerableDiagnosticValue(enumerable, depth);

		return FormatObjectDiagnosticValue(value, depth);
	}

	private static string FormatEnumerableDiagnosticValue(IEnumerable enumerable, int depth)
	{
		const int maxItems = 20;
		var items = new List<string>(maxItems);
		var count = 0;

		foreach (var item in enumerable)
		{
			if (count++ >= maxItems)
			{
				items.Add("...");
				break;
			}

			items.Add(FormatDiagnosticValue(item, depth + 1));
		}

		return $"[{string.Join(", ", items)}]";
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
