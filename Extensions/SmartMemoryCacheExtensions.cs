using System.Threading;
using Microsoft.Extensions.Primitives;

namespace CleverCache.Extensions
{
	public static class SmartMemoryCacheExtensions
    {
        /// <summary>
        /// Associate a value with a key in the <see cref="IMemoryCache"/>.
        /// </summary>
        /// <typeparam name="T">The type to which the cache key should be linked.</typeparam>
        /// <typeparam name="TItem">The type of the object to set.</typeparam>
        /// <param name="cache">The <see cref="IMemoryCache"/> instance this method extends.</param>
        /// <param name="key">The key of the entry to set.</param>
        /// <param name="value">The value to associate with the key.</param>
        /// <returns>The value that was set.</returns>
        public static TItem Set<T, TItem>(this ICleverCache cache, object key, TItem value) where T : class =>
            cache.Set(key, value, null as MemoryCacheEntryOptions);

        /// <summary>
        /// Sets a cache entry with the given key and value that will expire in the given duration.
        /// </summary>
        /// <typeparam name="T">The type to which the cache key should be linked.</typeparam>
        /// <typeparam name="TItem">The type of the object to set.</typeparam>
        /// <param name="cache">The <see cref="IMemoryCache"/> instance this method extends.</param>
        /// <param name="key">The key of the entry to set.</param>
        /// <param name="value">The value to associate with the key.</param>
        /// <param name="absoluteExpiration">The point in time at which the cache entry will expire.</param>
        /// <returns>The value that was set.</returns>
        public static TItem Set<T, TItem>(this ICleverCache cache,
            object key,
            TItem value,
            DateTimeOffset absoluteExpiration)
            where T : class =>
            cache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpiration = absoluteExpiration });

        /// <summary>
        /// Sets a cache entry with the given key and value that will expire in the given duration from now.
        /// </summary>
        /// <typeparam name="T">The type to which the cache key should be linked.</typeparam>
        /// <typeparam name="TItem">The type of the object to set.</typeparam>
        /// <param name="cache">The <see cref="IMemoryCache"/> instance this method extends.</param>
        /// <param name="key">The key of the entry to set.</param>
        /// <param name="value">The value to associate with the key.</param>
        /// <param name="absoluteExpirationRelativeToNow">The duration from now after which the cache entry will expire.</param>
        /// <returns>The value that was set.</returns>
        public static TItem Set<T, TItem>(this ICleverCache cache,
            object key,
            TItem value,
            TimeSpan absoluteExpirationRelativeToNow) where T : class =>
            cache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow });

        /// <summary>
        /// Sets a cache entry with the given key and value that will expire when <see cref="IChangeToken"/> expires.
        /// </summary>
        /// <typeparam name="T">The type to which the cache key should be linked.</typeparam>
        /// <typeparam name="TItem">The type of the object to set.</typeparam>
        /// <param name="cache">The <see cref="IMemoryCache"/> instance this method extends.</param>
        /// <param name="key">The key of the entry to set.</param>
        /// <param name="value">The value to associate with the key.</param>
        /// <param name="expirationToken">The <see cref="IChangeToken"/> that causes the cache entry to expire.</param>
        /// <returns>The value that was set.</returns>
        public static TItem Set<T, TItem>(this ICleverCache cache,
            object key,
            TItem value,
            IChangeToken expirationToken) where T : class =>
            cache.Set(key, value, new MemoryCacheEntryOptions { ExpirationTokens = { expirationToken } });

        /// <summary>
        /// Sets a cache entry with the given key and value and apply the values of an existing <see cref="MemoryCacheEntryOptions"/> to the created entry.
        /// </summary>
        /// <typeparam name="T">The type to which the cache key should be linked.</typeparam>
        /// <typeparam name="TItem">The type of the object to set.</typeparam>
        /// <param name="cache">The <see cref="IMemoryCache"/> instance this method extends.</param>
        /// <param name="key">The key of the entry to set.</param>
        /// <param name="value">The value to associate with the key.</param>
        /// <param name="options">The existing <see cref="MemoryCacheEntryOptions"/> instance to apply to the new entry.</param>
        /// <returns>The value that was set.</returns>
        public static TItem Set<T, TItem>(this ICleverCache cache, object key, TItem value, MemoryCacheEntryOptions? options) where T : class
        {
            cache.AddKeyToType<T>(key);
            return cache.Set(key, value, options);
        }

        /// <summary>
        /// Gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
        /// </summary>
        /// <typeparam name="T">The type of the object the cache key belongs to.</typeparam>
        /// <typeparam name="TItem">The type of the object to get.</typeparam>
        /// <param name="cache">The <see cref="IMemoryCache"/> instance this method extends.</param>
        /// <param name="key">The key of the entry to look for or create.</param>
        /// <param name="factory">The factory that creates the value associated with this key if the key does not exist in the cache.</param>
        /// <returns>The value associated with this key.</returns>
        public static TItem? GetOrCreate<T, TItem>(this ICleverCache cache, object key, Func<ICacheEntry, TItem> factory)
            where T : class => cache.GetOrCreate<T, TItem>(key, factory, null as MemoryCacheEntryOptions);


        /// <summary>
        /// Asynchronously gets the value associated with this key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
        /// </summary>
        /// <typeparam name="T">The type to which the cache key should be linked.</typeparam>
        /// <typeparam name="TItem">The type of the object to get.</typeparam>
        /// <param name="cache">The <see cref="IMemoryCache"/> instance this method extends.</param>
        /// <param name="key">The key of the entry to look for or create.</param>
        /// <param name="factory">The factory task that creates the value associated with this key if the key does not exist in the cache.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public static Task<TItem?> GetOrCreateAsync<T, TItem>(this ICleverCache cache,
            object key,
            Func<ICacheEntry, Task<TItem>> factory) where T : class =>
            cache.GetOrCreateAsync<T, TItem>(key, factory, null as MemoryCacheEntryOptions);
    }
}
