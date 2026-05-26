namespace CleverCache.EntityFrameworkCore.Extensions;

public static class DbOptionsBuilderExtensions
{
	/// <summary>
	/// Adds <see cref="CleverCacheInterceptor"/> to the <see cref="DbContextOptionsBuilder"/>.
	/// Use this when you prefer constructor injection over the DI-based interceptor registration.
	/// </summary>
	public static DbContextOptionsBuilder AddCleverCache(this DbContextOptionsBuilder optionsBuilder,
		IServiceProvider serviceProvider)
	{
		var interceptor = serviceProvider.GetRequiredService<CleverCacheInterceptor>();
		optionsBuilder.AddInterceptors(interceptor);
		return optionsBuilder;
	}
}
