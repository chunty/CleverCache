namespace CleverCache.Exceptions;

internal class MissingInterceptorException() :
	Exception("CleverCache requires the ClearSmartMemoryCacheInterceptor to be added to the database context.");