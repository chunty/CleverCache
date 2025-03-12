using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable IdentifierTypo

namespace CleverCache.DependencyInjection
{
	public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCleverCache(this IServiceCollection services,
            Action<CleverCacheOptions>? options = null)
        {
            // Register the Smart Cache Options
            var localOptions = new CleverCacheOptions();
            options?.Invoke(localOptions);
            services.TryAddSingleton(localOptions);

            services.AddMemoryCache();

            // Register ICleverCache
            services.TryAddSingleton<ICleverCache, CleverMemoryCache>();

            // Register the Smart Cache Interceptor as Service
            services.TryAddSingleton<CleverCacheInterceptor>();

            // Register the Smart Cache Interceptor as Interceptor
            services.AddSingleton<IInterceptor, CleverCacheInterceptor>();

            return services;
        }
    }
}