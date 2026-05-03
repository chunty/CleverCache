using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CleverCache.EntityFrameworkCore.DependencyInjection;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers <see cref="CleverCacheInterceptor"/> so it can be injected into your <see cref="DbContext"/>.
	/// Call this alongside <c>AddCleverCache()</c> when using EF Core.
	/// </summary>
	public static IServiceCollection AddCleverCacheEntityFramework(this IServiceCollection services)
	{
		services.TryAddScoped<CleverCacheInterceptor>();
		services.AddScoped<IInterceptor, CleverCacheInterceptor>();
		return services;
	}
}
