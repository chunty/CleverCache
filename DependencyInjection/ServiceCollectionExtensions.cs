using CleverCache.Implementations;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable IdentifierTypo

namespace CleverCache.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddCleverCache(this IServiceCollection services,
		Action<CleverCacheOptions>? options = null)
	{
		var localOptions = new CleverCacheOptions();
		options?.Invoke(localOptions);
		services.TryAddSingleton(localOptions);

		// Register chosen store (defaults to memory)
		localOptions.StoreRegistration?.Invoke(services);

		// Register ICleverCache backed by the store
		services.TryAddSingleton<ICleverCache, CleverCacheService>();

		return services;
	}
}