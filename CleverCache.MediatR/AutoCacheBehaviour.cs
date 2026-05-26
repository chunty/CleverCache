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
			() => next(cancellationToken),
			cancellationToken: cancellationToken
		);

		// GetOrCreateAsync returns TResponse? to satisfy nullability constraints on the cache store,
		// but the handler is responsible for its own return value — if it legitimately returns null,
		// propagate that rather than re-executing the handler.
		return result ?? default!;
	}
}
