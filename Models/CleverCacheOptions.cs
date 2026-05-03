using System.Reflection;
using CleverCache.Attributes;
using CleverCache.Implementations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleverCache.Models;

public class CleverCacheOptions(HashSet<DependentCache>? dependentCaches = null)
{
	public HashSet<DependentCache> DependentCaches { get; set; } = dependentCaches ?? [];

	/// <summary>
	/// Scans the assembly containing <typeparamref name="T"/> for <see cref="DependentCachesAttribute"/>
	/// and registers the declared cache dependency relationships.
	/// Use this instead of relying on <c>UseCleverCache&lt;TContext&gt;()</c> for attribute-based configuration.
	/// </summary>
	/// <example>
	/// <code>
	/// builder.Services.AddCleverCache(o => o.ScanAssemblyContaining&lt;Order&gt;());
	/// </code>
	/// </example>
	public CleverCacheOptions ScanAssemblyContaining<T>() => ScanAssemblies(typeof(T).Assembly);

	/// <summary>
	/// Scans the specified assemblies for <see cref="DependentCachesAttribute"/> and registers
	/// the declared cache dependency relationships.
	/// </summary>
	public CleverCacheOptions ScanAssemblies(params Assembly[] assemblies)
	{
		foreach (var assembly in assemblies)
		{
			foreach (var type in assembly.GetTypes())
			{
				var attr = type.GetCustomAttribute<DependentCachesAttribute>();
				if (attr is null) continue;

				foreach (var depType in attr.DependantTypes)
				{
					DependentCaches.Add(new DependentCache(type, depType));
					if (attr.Reverse)
						DependentCaches.Add(new DependentCache(depType, type));
				}
			}
		}
		return this;
	}

	internal Action<IServiceCollection> StoreRegistration { get; private set; } = services =>
	{
		services.AddMemoryCache();
		services.TryAddSingleton<ICleverCacheStore, MemoryCacheStore>();
	};

	/// <summary>Uses the built-in <see cref="IMemoryCache"/> backend (default).</summary>
	public CleverCacheOptions UseMemoryCache()
	{
		StoreRegistration = services =>
		{
			services.AddMemoryCache();
			services.TryAddSingleton<ICleverCacheStore, MemoryCacheStore>();
		};
		return this;
	}

	/// <summary>
	/// Uses the built-in <see cref="IDistributedCache"/> backend.
	/// Requires <c>IDistributedCache</c> to be registered (e.g. via <c>AddStackExchangeRedisCache</c> or <c>AddDistributedMemoryCache</c>).
	/// </summary>
	public CleverCacheOptions UseDistributedCache()
	{
		StoreRegistration = services =>
			services.TryAddSingleton<ICleverCacheStore, DistributedCacheStore>();
		return this;
	}

	/// <summary>Registers a custom <see cref="ICleverCacheStore"/> implementation.</summary>
	public CleverCacheOptions UseCustomStore<TStore>() where TStore : class, ICleverCacheStore
	{
		StoreRegistration = services =>
			services.TryAddSingleton<ICleverCacheStore, TStore>();
		return this;
	}

	/// <summary>Registers a custom <see cref="ICleverCacheStore"/> via a factory.</summary>
	public CleverCacheOptions UseCustomStore(Func<IServiceProvider, ICleverCacheStore> factory)
	{
		StoreRegistration = services =>
			services.TryAddSingleton<ICleverCacheStore>(factory);
		return this;
	}

	/// <summary>
	/// Provides direct control over the service registrations used for the cache store.
	/// Intended for use by extension packages (e.g. CleverCache.Redis) that need to register
	/// additional services alongside the <see cref="ICleverCacheStore"/>.
	/// </summary>
	public CleverCacheOptions UseCustomStoreRegistration(Action<IServiceCollection> registration)
	{
		StoreRegistration = registration;
		return this;
	}
}
