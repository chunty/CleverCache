using System.Text.Json;

namespace CleverCache;

internal static class CacheKeyIdentity
{
	private const string Prefix = "cck::";
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	internal static string ToCanonicalKey(object key)
	{
		ArgumentNullException.ThrowIfNull(key);

		if (key is string s && IsCanonicalKey(s))
			return s;

		var typeIdentity = GetTypeIdentity(key.GetType());
		var payload = SerializePayload(key);
		return $"{Prefix}{typeIdentity}|{payload}";
	}

	internal static bool IsCanonicalKey(string key) =>
		key.StartsWith(Prefix, StringComparison.Ordinal);

	internal static string GetTypeIdentity(Type type)
	{
		var fullName = type.FullName ?? type.Name;
		var assemblyName = type.Assembly.GetName().Name ?? "UnknownAssembly";
		return $"{fullName}, {assemblyName}";
	}

	private static string SerializePayload(object key)
	{
		if (key is Type typeKey)
			return JsonSerializer.Serialize(GetTypeIdentity(typeKey), JsonOptions);

		if (key is Delegate del)
			return JsonSerializer.Serialize(del.ToString(), JsonOptions);

		try
		{
			return JsonSerializer.Serialize(key, key.GetType(), JsonOptions);
		}
		catch (NotSupportedException)
		{
			return JsonSerializer.Serialize(key.ToString(), JsonOptions);
		}
		catch (InvalidOperationException)
		{
			return JsonSerializer.Serialize(key.ToString(), JsonOptions);
		}
	}
}
