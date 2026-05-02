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

		var attribute = typeof(TRequest).GetCustomAttribute<AutoCacheAttribute>();

		if (attribute is null)
		{
			return await next(cancellationToken);
		}

		var result = await cache.GetOrCreateAsync(
			attribute.Types,
			request,
			() => next(cancellationToken)
		);

		if (result is not null)
		{
			return result;
		}

		cache.Remove(request);
		return await next(cancellationToken);
	}
}
