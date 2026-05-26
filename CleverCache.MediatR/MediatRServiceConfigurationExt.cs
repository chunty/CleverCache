using Microsoft.Extensions.DependencyInjection;

namespace CleverCache.Mediatr;

public static class MediatRServiceConfigurationExt
{
	public static void AddCleverCache(this MediatRServiceConfiguration cfg)
	{
		cfg.AddOpenBehavior(typeof(InvalidateCacheBehaviour<,>));
		cfg.AddOpenBehavior(typeof(AutoCacheBehaviour<,>));
	}
}
