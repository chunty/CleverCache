using System.Reflection;
using MediatR;

namespace CleverCache.Mediatr;
internal class AutoCacheBehaviour<TRequest, TResponse>(ICleverCache cache)
	: IPipelineBehavior<TRequest, TResponse>
	where TRequest : class
{
	public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(next);

		// Get all interfaces implemented by TRequest
		var attribute = typeof(TRequest).GetCustomAttribute<AutoCacheAttribute>();

		// No types to use 
		if (attribute is null)
		{
			return await next(cancellationToken);
		}

		// Try to get the result from cache
		var result = await cache.GetOrCreateAsync(
			attribute.Types,
			request,
			async _ =>
			{
				var result = await next(cancellationToken);
				return result;
			}
			// Call next only once when the cache is not available
		);

		// If cache has the result, return it
		if (result is not null)
		{
			return result;
		}

		// If result is null, remove from cache and call next delegate
		cache.Remove(request);
		return await next(cancellationToken); // Only call next here if cache miss
	}
}
