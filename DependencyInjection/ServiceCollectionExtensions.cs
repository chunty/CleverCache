using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable IdentifierTypo

namespace SmartCache.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSmartCache(this IServiceCollection services,
            Action<SmartCacheOptions>? options = null)
        {
            // Register the Smart Cache Options
            var localOptions = new SmartCacheOptions();
            options?.Invoke(localOptions);
            services.TryAddSingleton(localOptions);

            services.AddMemoryCache();

            // Register ISmartCache
            services.TryAddSingleton<ISmartCache, SmartMemoryCache>();

            // Register the Smart Cache Interceptor as Service
            services.TryAddSingleton<ClearSmartMemoryCacheInterceptor>();

            // Register the Smart Cache Interceptor as Interceptor
            services.AddSingleton<IInterceptor, ClearSmartMemoryCacheInterceptor>();

            return services;
        }
    }
}