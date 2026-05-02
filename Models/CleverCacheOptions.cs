using CleverCache.Implementations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleverCache.Models;

public class CleverCacheOptions(CleverCacheScanOptions? scanOptions = null,
	HashSet<DependentCache>? dependentCaches = null,
	bool disableAllScanning = false)
{
	// ReSharper disable once IdentifierTypo
	public CleverCacheScanOptions Scanning { get; set; } = scanOptions ?? new CleverCacheScanOptions();
	public HashSet<DependentCache> DependentCaches { get; set; } = dependentCaches ?? [];
	public bool DisableAllScanning { get; set; } = disableAllScanning;

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
