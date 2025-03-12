namespace CleverCache.Extensions
{
    public static class DbOptionsBuilderExtensions
    {
        public static DbContextOptionsBuilder AddCleverCache(this DbContextOptionsBuilder optionsBuilder, IServiceProvider serviceProvider)
        {
            var interceptor = serviceProvider.GetRequiredService<CleverCacheInterceptor>();

            optionsBuilder.AddInterceptors(interceptor);
            return optionsBuilder;
        }
    }

}
