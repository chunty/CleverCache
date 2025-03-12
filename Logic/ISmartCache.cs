namespace CleverCache.Logic
{
    public interface ICleverCache : IMemoryCache
    {
        /// <summary>
        /// Adds a dependent cache type.
        /// </summary>
        /// <param name="type">The type of the cache.</param>
        /// <param name="dependentType">The dependent type of the cache.</param>
        void AddDependentCache(Type type, Type dependentType);

        /// <typeparam name="T">The type of the cache.</typeparam>
        /// <see cref="CleverMemoryCache.AddDependentCache"/>
        void AddDependentCache<T>(Type dependentType);

        /// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
        /// <see cref="CleverMemoryCache.AddKeyToEntryType"/>>
        void AddKeyToEntryType<T>(object key) where T : class;

        /// <summary>
        /// Adds the specified key to the cache entry type.
        /// </summary>
        /// <param name="type">The type of the object the cache key belongs to.</param>
        /// <param name="key">The key of the cache entry to add.</param>
        void AddKeyToEntryType(Type type, object key);

        /// <summary>
        /// Removes all cache entries of the specified type.
        /// </summary>
        /// <param name="type">The type of the objects to remove cache entries for.</param>
        void RemoveTypeKeys(Type type);

        /// <summary>
        /// Creates a new cache entry with the specified key.
        /// </summary>
        /// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
        /// <param name="key">The key of the cache entry to create.</param>
        /// <returns>The created cache entry.</returns>
        ICacheEntry CreateEntry<T>(object key) where T : class;

        /// <summary>
        /// Gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
        /// </summary>
        /// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
        /// <typeparam name="TItem">The type of the object to get.</typeparam>
        /// <param name="key">The key of the entry to look for or create.</param>
        /// <param name="factory">The factory that creates the value associated with this key if the key does not exist in the cache.</param>
        /// <param name="createOptions">The options to be applied to the <see cref="ICacheEntry"/> if the key does not exist in the cache.</param>
        /// <returns>The value associated with this key.</returns>
        TItem? GetOrCreate<T, TItem>(object key,
            Func<ICacheEntry, TItem> factory,
            MemoryCacheEntryOptions? createOptions) where T : class;

        /// <summary>
        /// Asynchronously gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
        /// </summary>
        /// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
        /// <typeparam name="TItem">The type of the object to get.</typeparam>
        /// <param name="key">The key of the entry to look for or create.</param>
        /// <param name="factory">The factory task that creates the value associated with this key if the key does not exist in the cache.</param>
        /// <param name="createOptions">The options to be applied to the <see cref="ICacheEntry"/> if the key does not exist in the cache.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<TItem?> GetOrCreateAsync<T, TItem>(object key,
            Func<ICacheEntry, Task<TItem>> factory,
            MemoryCacheEntryOptions? createOptions) where T : class;
    }
}