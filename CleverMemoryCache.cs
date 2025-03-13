using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Reflection.Metadata;

namespace CleverCache;

/// <inheritdoc cref="ICleverCache"/>
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

	/// <inheritdoc />
	public void AddDependentCache(Type type, Type dependentType) =>
		_dependentCaches.Add(new DependentCache(type, dependentType));

	/// <inheritdoc />
	public void AddDependentCache<T>(Type dependentType) => AddDependentCache(typeof(T), dependentType);

	/// <inheritdoc />
	public void AddKeyToType<T>(object key) where T : class => AddKeyToType(typeof(T), key);

	/// <inheritdoc />
	public void AddKeyToType(Type type, object key) => AddKeyToTypes([type], key);

	/// <inheritdoc />
	public void AddKeyToTypes(Type[] types, object key)
	{
		foreach (var type in types)
		{
			_cacheEntries.Add(new CacheEntry(type, key));
			foreach (var dependentCache in _dependentCaches.Where(x => x.Type == type))
			{
				AddKeyToType(dependentCache.DependentType, key);
			}
		}
	}

	/// <inheritdoc />
	public void RemoveTypeKeys(Type type)
	{
		foreach (var entry in _cacheEntries.Where(x => x.Type == type))
		{
			Remove(entry.Key);
		}
	}

	/// <inheritdoc />
	public ICacheEntry CreateEntry<T>(object key) where T : class => CreateEntry(typeof(T), key);

	/// <inheritdoc />
	public ICacheEntry CreateEntry(Type type, object key) => CreateEntry([type], key);

	/// <inheritdoc />
	public ICacheEntry CreateEntry(Type[] types, object key)
	{
		var result = CreateEntry(key);
		AddKeyToTypes(types, key);
		return result;
	}

	/// <inheritdoc />
	public TItem? GetOrCreate<T, TItem>(object key,
		Func<ICacheEntry, TItem> factory,
		MemoryCacheEntryOptions? createOptions = null) where T : class =>
		GetOrCreate(typeof(T), key, factory, createOptions);

	public TItem? GetOrCreate<TItem>(Type type, object key, Func<ICacheEntry, TItem> factory,
		MemoryCacheEntryOptions? createOptions = null) =>
		GetOrCreate([type], key, factory, createOptions);

	/// <inheritdoc />
	public TItem? GetOrCreate<TItem>(Type[] types, object key, Func<ICacheEntry, TItem> factory, MemoryCacheEntryOptions? createOptions = null)
	{
		if (TryGetValue(key, out var result))
		{
			return (TItem?)result;
		}

		try
		{
			// Prevent race conditions
			_semaphore.Wait();

			using var entry = CreateEntry(types, key);

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

	/// <inheritdoc />
	public async Task<TItem?> GetOrCreateAsync<T, TItem>(object key,
		Func<ICacheEntry, Task<TItem>> factory,
		MemoryCacheEntryOptions? createOptions = null) where T : class =>
		await GetOrCreateAsync(typeof(T), key, factory, createOptions);

	/// <inheritdoc />
	public async Task<TItem?> GetOrCreateAsync<TItem>(Type type, 
		object key, 
		Func<ICacheEntry, Task<TItem>> factory, 
		MemoryCacheEntryOptions? createOptions = null) =>
		await GetOrCreateAsync([type], key, factory, createOptions);

	/// <inheritdoc />
	public async Task<TItem?> GetOrCreateAsync<TItem>(Type[] types, 
		object key, 
		Func<ICacheEntry, Task<TItem>> factory, 
		MemoryCacheEntryOptions? createOptions = null)
	{
		if (TryGetValue(key, out var result))
		{
			return (TItem?)result;
		}

		try
		{
			// Prevent race conditions
			await _semaphore.WaitAsync();

			using var entry = CreateEntry(types, key);

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