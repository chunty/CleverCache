namespace CleverCache.EntityFrameworkCore.Exceptions;

internal class MissingInterceptorException() :
	Exception("CleverCache requires CleverCacheInterceptor to be registered. Call AddCleverCacheEntityFramework() on your IServiceCollection.");
