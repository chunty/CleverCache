namespace SmartCache.Exceptions
{
    internal class MissingSmartCacheInterceptorException() :
        ApplicationException("SmartCache requires the ClearSmartMemoryCacheInterceptor to be added to the database context.");
}