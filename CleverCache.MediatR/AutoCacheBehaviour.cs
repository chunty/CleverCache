using CleverCache.Models;
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

		var createOptions = BuildOptions(attribute);
		var result = await cache.GetOrCreateAsync(
			attribute.Types,
			request,
			() => next(cancellationToken),
			createOptions,
			cancellationToken: cancellationToken
		);

		// GetOrCreateAsync returns TResponse? to satisfy nullability constraints on the cache store,
		// but the handler is responsible for its own return value — if it legitimately returns null,
		// propagate that rather than re-executing the handler.
		return result ?? default!;
	}

	private static CleverCacheEntryOptions? BuildOptions(AutoCacheAttribute attribute)
	{
		if (attribute.SlidingExpirationSeconds <= 0 && attribute.AbsoluteExpirationSeconds <= 0)
			return null;

		var options = new CleverCacheEntryOptions();

		if (attribute.SlidingExpirationSeconds > 0)
		{
			options.SlidingExpiration = TimeSpan.FromSeconds(attribute.SlidingExpirationSeconds);
		}

		if (attribute.AbsoluteExpirationSeconds > 0)
		{
			options.AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(attribute.AbsoluteExpirationSeconds);
		}

		return options;
	}
}
