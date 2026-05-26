using System.Reflection;
using MediatR;

namespace CleverCache.Mediatr;

internal class InvalidateCacheBehaviour<TRequest, TResponse>(ICleverCache cache)
	: IPipelineBehavior<TRequest, TResponse>
	where TRequest : class
{
	public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(next);

		var attribute = typeof(TRequest).GetCustomAttribute<InvalidatesCacheAttribute>();

		if (attribute is null)
			return await next(cancellationToken);

		var result = await next(cancellationToken);

		foreach (var type in attribute.Types)
			await cache.RemoveByTypeAsync(type, cancellationToken);

		return result;
	}
}
