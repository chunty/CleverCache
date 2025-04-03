namespace CleverCache.Mediatr;
public static class MediatRServiceConfigurationExt
{
	public static void AddCleverCache(this MediatRServiceConfiguration cfg)
	{
		cfg.AddOpenBehavior(typeof(AutoCacheBehaviour<,>));
	}
}
