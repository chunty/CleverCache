using Microsoft.Extensions.DependencyInjection;

namespace CleverCache.Mediatr;
internal static class MediatRServiceConfigurationExt
{
	private static void AddCleverCache(this MediatRServiceConfiguration cfg)
	{
		cfg.AddOpenBehavior(typeof(AutoCacheBehaviour<,>));
	}
}
