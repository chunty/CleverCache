using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CleverCache;

internal static class CacheKeyIdentity
{
	private const string Prefix = "cck::";
	private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

	internal static string ToCanonicalKey(object key)
	{
		ArgumentNullException.ThrowIfNull(key);

		if (key is string s && IsCanonicalKey(s))
			return s;

		var typeIdentity = GetTypeIdentity(key.GetType());
		return ToCanonicalKey(typeIdentity, key);
	}

	internal static string ToCanonicalKey(string typeIdentity, object payload)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(typeIdentity);
		ArgumentNullException.ThrowIfNull(payload);

		var serializedPayload = SerializePayload(payload);
		return $"{Prefix}{typeIdentity}|{serializedPayload}";
	}

	internal static bool TryToCanonicalKey(object? key, out string canonicalKey)
	{
		if (key is null)
		{
			canonicalKey = string.Empty;
			return false;
		}

		try
		{
			canonicalKey = ToCanonicalKey(key);
			return true;
		}
		catch (NotSupportedException)
		{
			canonicalKey = string.Empty;
			return false;
		}
		catch (InvalidOperationException)
		{
			canonicalKey = string.Empty;
			return false;
		}
		catch (ArgumentException)
		{
			canonicalKey = string.Empty;
			return false;
		}
	}

	internal static bool TryToCanonicalKey(string typeIdentity, object? payload, out string canonicalKey)
	{
		if (payload is null || string.IsNullOrWhiteSpace(typeIdentity))
		{
			canonicalKey = string.Empty;
			return false;
		}

		try
		{
			canonicalKey = ToCanonicalKey(typeIdentity, payload);
			return true;
		}
		catch (NotSupportedException)
		{
			canonicalKey = string.Empty;
			return false;
		}
		catch (InvalidOperationException)
		{
			canonicalKey = string.Empty;
			return false;
		}
		catch (ArgumentException)
		{
			canonicalKey = string.Empty;
			return false;
		}
	}

	internal static bool TryGetUnsupportedKeyShapeReason(object? key, out string? reason)
	{
		if (key is null)
		{
			reason = null;
			return false;
		}

		var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
		return TryGetUnsupportedKeyShapeReason(key, "key", 0, visited, out reason);
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

	private static bool TryGetUnsupportedKeyShapeReason(object key, string path, int depth, HashSet<object> visited, out string? reason)
	{
		reason = null;

		var type = key.GetType();
		if (IsSupportedLeafType(type))
			return false;

		if (key is Delegate)
		{
			reason = $"{path} contains a delegate ({GetTypeIdentity(type)})";
			return true;
		}

		if (key is Expression)
		{
			reason = $"{path} contains an expression ({GetTypeIdentity(type)})";
			return true;
		}

		if (typeof(IQueryable).IsAssignableFrom(type))
		{
			reason = $"{path} contains a queryable value ({GetTypeIdentity(type)})";
			return true;
		}

		if (depth >= 6)
			return false;

		if (!type.IsValueType && !visited.Add(key))
			return false;

		if (key is IEnumerable enumerable && key is not string)
		{
			var index = 0;
			foreach (var item in enumerable)
			{
				if (item is null)
				{
					index++;
					continue;
				}

				if (TryGetUnsupportedKeyShapeReason(item, $"{path}[{index}]", depth + 1, visited, out reason))
					return true;

				index++;
			}

			return false;
		}

		foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
		{
			if (!property.CanRead || property.GetIndexParameters().Length > 0)
				continue;

			var value = property.GetValue(key);
			if (value is not null &&
				TryGetUnsupportedKeyShapeReason(value, $"{path}.{property.Name}", depth + 1, visited, out reason))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsSupportedLeafType(Type type) =>
		type.IsPrimitive ||
		type.IsEnum ||
		type == typeof(string) ||
		type == typeof(decimal) ||
		type == typeof(DateTime) ||
		type == typeof(DateTimeOffset) ||
		type == typeof(TimeSpan) ||
		type == typeof(Guid) ||
		type == typeof(Uri) ||
		type == typeof(Type) ||
		type == typeof(Version) ||
		type == typeof(DateOnly) ||
		type == typeof(TimeOnly) ||
		type == typeof(IntPtr) ||
		type == typeof(UIntPtr);

	private static JsonSerializerOptions CreateJsonOptions()
	{
		var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
		options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
		options.Converters.Add(new ExpressionJsonConverterFactory());
		return options;
	}

	private sealed class ExpressionJsonConverterFactory : JsonConverterFactory
	{
		public override bool CanConvert(Type typeToConvert) =>
			typeof(Expression).IsAssignableFrom(typeToConvert);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var converterType = typeof(ExpressionJsonConverter<>).MakeGenericType(typeToConvert);
			return (JsonConverter)Activator.CreateInstance(converterType)!;
		}
	}

	private sealed class ExpressionJsonConverter<TExpression> : JsonConverter<TExpression>
		where TExpression : Expression
	{
		public override TExpression? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			throw new NotSupportedException("Cache key expressions are write-only.");

		public override void Write(Utf8JsonWriter writer, TExpression value, JsonSerializerOptions options) =>
			writer.WriteStringValue(value.ToString());
	}
}
