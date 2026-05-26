using CleverCache.Implementations;
using CleverCache.Models;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleverCache.Redis;

public static class CleverCacheOptionsRedisExtensions
{
	/// <summary>
	/// Configures CleverCache to use Redis as the cache backend via StackExchange.Redis.
	/// </summary>
	/// <param name="options">The CleverCache options.</param>
	/// <param name="connectionString">The Redis connection string (e.g. "localhost:6379").</param>
	public static CleverCacheOptions UseRedisCache(this CleverCacheOptions options, string connectionString) =>
		options.UseRedisCache(redis => redis.Configuration = connectionString);

	/// <summary>
	/// Configures CleverCache to use Redis as the cache backend via StackExchange.Redis.
	/// </summary>
	/// <param name="options">The CleverCache options.</param>
	/// <param name="configure">A delegate to configure the Redis cache options.</param>
	public static CleverCacheOptions UseRedisCache(this CleverCacheOptions options, Action<RedisCacheOptions> configure) =>
		options.UseCustomStoreRegistration(services =>
		{
			services.AddStackExchangeRedisCache(configure);
			services.TryAddSingleton<ICleverCacheStore, DistributedCacheStore>();
		});
}
