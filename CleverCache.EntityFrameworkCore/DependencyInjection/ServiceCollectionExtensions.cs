using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CleverCache.EntityFrameworkCore.DependencyInjection;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers CleverCache services and the EF Core <see cref="CleverCacheInterceptor"/>.
	/// This is the single call needed when using EF Core — there is no need to also call <c>AddCleverCache()</c>.
	/// </summary>
	public static IServiceCollection AddCleverCacheEntityFramework(this IServiceCollection services,
		Action<CleverCacheOptions>? options = null)
	{
		services.AddCleverCache(options);
		services.TryAddScoped<CleverCacheInterceptor>();
		services.AddScoped<IInterceptor, CleverCacheInterceptor>();
		return services;
	}
}
