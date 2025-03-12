using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleverCache.Logic
{
    /// <summary>
    /// A smart memory cache that extends the <see cref="MemoryCache"/> and implements <see cref="ICleverCache"/>.
    /// </summary>
    public class CleverMemoryCache : MemoryCache, ICleverCache
    {
        private readonly HashSet<CacheEntry> _cacheEntries = [];
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly HashSet<DependentCache> _dependentCaches = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="CleverMemoryCache"/> class with the specified options.
        /// </summary>
        /// <param name="optionsAccessor">The options to configure the memory cache.</param>
        public CleverMemoryCache(IOptions<MemoryCacheOptions> optionsAccessor) : base(optionsAccessor) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CleverMemoryCache"/> class with the specified options and logger factory.
        /// </summary>
        /// <param name="optionsAccessor">The options to configure the memory cache.</param>
        /// <param name="loggerFactory">The logger factory to create loggers.</param>
        public CleverMemoryCache(IOptions<MemoryCacheOptions> optionsAccessor, ILoggerFactory loggerFactory) : base(optionsAccessor, loggerFactory) { }

        /// <summary>
        /// Adds a dependent cache type.
        /// </summary>
        /// <param name="type">The type of the cache.</param>
        /// <param name="dependentType">The dependent type of the cache.</param>
        public void AddDependentCache(Type type, Type dependentType) =>
            _dependentCaches.Add(new DependentCache(type, dependentType));

        /// <typeparam name="T">The type of the cache.</typeparam>
        /// <see cref="AddDependentCache"/>
        public void AddDependentCache<T>(Type dependentType) => AddDependentCache(typeof(T), dependentType);

        /// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
        /// <see cref="AddKeyToEntryType"/>>
        public void AddKeyToEntryType<T>(object key) where T : class => AddKeyToEntryType(typeof(T), key);

        /// <summary>
        /// Adds the specified key to the cache entry type.
        /// </summary>
        /// <param name="type">The type of the object the cache key belongs to.</param>
        /// <param name="key">The key of the cache entry to add.</param>
        public void AddKeyToEntryType(Type type, object key)
        {
            _cacheEntries.Add(new CacheEntry(type, key));
            foreach (var dependentCache in _dependentCaches.Where(x => x.Type == type))
            {
                AddKeyToEntryType(dependentCache.DependentType, key);
            }
        }

        /// <summary>
        /// Removes all cache entries of the specified type.
        /// </summary>
        /// <param name="type">The type of the objects to remove cache entries for.</param>
        public void RemoveTypeKeys(Type type)
        {
            foreach (var entry in _cacheEntries.Where(x => x.Type == type))
            {
                base.Remove(entry.Key);
            }
        }

        /// <summary>
        /// Creates a new cache entry with the specified key.
        /// </summary>
        /// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
        /// <param name="key">The key of the cache entry to create.</param>
        /// <returns>The created cache entry.</returns>
        public ICacheEntry CreateEntry<T>(object key) where T : class
        {
            var result = base.CreateEntry(key);
            AddKeyToEntryType<T>(key);
            return result;
        }

        /// <summary>
        /// Gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
        /// </summary>
        /// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
        /// <typeparam name="TItem">The type of the object to get.</typeparam>
        /// <param name="key">The key of the entry to look for or create.</param>
        /// <param name="factory">The factory that creates the value associated with this key if the key does not exist in the cache.</param>
        /// <param name="createOptions">The options to be applied to the <see cref="ICacheEntry"/> if the key does not exist in the cache.</param>
        /// <returns>The value associated with this key.</returns>
        public TItem? GetOrCreate<T, TItem>(object key,
            Func<ICacheEntry, TItem> factory,
            MemoryCacheEntryOptions? createOptions) where T : class
        {
            if (this.TryGetValue(key, out object? result))
            {
                return (TItem?)result;
            }

            try
            {
                // Prevent race conditions
                _semaphore.Wait();

                using ICacheEntry entry = this.CreateEntry<T>(key);

                if (createOptions != null)
                {
                    entry.SetOptions(createOptions);
                }

                result = factory(entry);
                entry.Value = result;

                return (TItem?)result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Asynchronously gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
        /// </summary>
        /// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
        /// <typeparam name="TItem">The type of the object to get.</typeparam>
        /// <param name="key">The key of the entry to look for or create.</param>
        /// <param name="factory">The factory task that creates the value associated with this key if the key does not exist in the cache.</param>
        /// <param name="createOptions">The options to be applied to the <see cref="ICacheEntry"/> if the key does not exist in the cache.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<TItem?> GetOrCreateAsync<T, TItem>(object key,
            Func<ICacheEntry, Task<TItem>> factory,
            MemoryCacheEntryOptions? createOptions) where T : class
        {
            if (this.TryGetValue(key, out object? result))
            {
                return (TItem?)result;
            }

            try
            {
                // Prevent race conditions
                await _semaphore.WaitAsync();

                using ICacheEntry entry = this.CreateEntry<T>(key);

                if (createOptions != null)
                {
                    entry.SetOptions(createOptions);
                }

                result = await factory(entry).ConfigureAwait(false);
                entry.Value = result;

                return (TItem?)result;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}