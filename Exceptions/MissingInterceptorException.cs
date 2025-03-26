namespace CleverCache.Exceptions;

internal class MissingInterceptorException() :
	ApplicationException("CleverCache requires the ClearSmartMemoryCacheInterceptor to be added to the database context.");