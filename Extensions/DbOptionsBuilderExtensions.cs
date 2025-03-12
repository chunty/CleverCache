namespace SmartCache.Extensions
{
    public static class DbOptionsBuilderExtensions
    {
        public static DbContextOptionsBuilder AddSmartCache(this DbContextOptionsBuilder optionsBuilder, IServiceProvider serviceProvider)
        {
            var interceptor = serviceProvider.GetRequiredService<ClearSmartMemoryCacheInterceptor>();

            optionsBuilder.AddInterceptors(interceptor);
            return optionsBuilder;
        }
    }

}
