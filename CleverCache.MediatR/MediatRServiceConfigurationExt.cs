using MediatR;
using MediatR.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace CleverCache.Mediatr;

public static class MediatRServiceConfigurationExt
{
	public static void AddCleverCache(this MediatRServiceConfiguration cfg)
	{
		cfg.AddOpenBehavior(typeof(AutoCacheBehaviour<,>));
	}
}
