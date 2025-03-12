namespace CleverCache.Exceptions
{
    internal class MissingCleverCacheInterceptorException() :
        ApplicationException("CleverCache requires the ClearSmartMemoryCacheInterceptor to be added to the database context.");
}