// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.DAL.Redis.Interfaces;
using EchoPhase.Types.Extensions;
using Microsoft.Extensions.Caching.Distributed;

namespace EchoPhase.DAL.Redis.Database
{
    public class CacheEntry<T> : ICacheEntry<T>
    {
        private readonly IDistributedCache _cache;

        public CacheEntry(
            IDistributedCache cache,
            string cacheKey,
            Guid tenantId,
            string key)
        {
            _cache = cache;
            CacheKey = cacheKey;
            TenantId = tenantId;
            Key = key;
        }

        public Guid TenantId { get; }
        public string Key { get; }
        public string CacheKey { get; }

        public virtual async Task<T> GetOrSetAsync(Func<Task<T>> getData, TimeSpan cacheDuration)
        {
            try
            {
                return await GetAsync();
            }
            catch (InvalidOperationException)
            {
                return await SetAsync(getData, cacheDuration);
            }
        }

        public virtual async Task<T> GetAsync()
        {
            string? cachedData = await _cache.GetStringAsync(CacheKey) ??
                throw new InvalidOperationException($"Data not found in cache for tenant {TenantId}, key {Key} (cache key: {CacheKey}).");

            try
            {
                T? result = JsonSerializer.Deserialize<T>(cachedData);
                if (EqualityComparer<T>.Default.Equals(result, default(T)))
                    throw new InvalidOperationException($"Unable to deserialize result for tenant {TenantId}, key {Key}.");
                if (result is null)
                    throw new InvalidOperationException($"Data is null for tenant {TenantId}, key {Key}.");
                return result;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Error deserializing data for tenant {TenantId}, key {Key}: {ex.Message}.", ex);
            }
        }

        public virtual async Task<T> SetAsync(Func<Task<T>> getData, TimeSpan cacheDuration)
        {
            T data = await getData();
            await SetAsync(data, cacheDuration);
            return data;
        }

        public virtual async Task SetAsync(T data, TimeSpan cacheDuration)
        {
            string serializedData = JsonSerializer.Serialize(data);
            await _cache.SetStringAsync(CacheKey, serializedData, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheDuration
            });
        }

        public virtual async Task<T> SetAsync(Func<T> getData, TimeSpan cacheDuration) =>
            await SetAsync(getData.ToAsync(), cacheDuration);

        public virtual async Task<T> GetOrSetAsync(Func<T> getData, TimeSpan cacheDuration) =>
            await GetOrSetAsync(getData.ToAsync(), cacheDuration);

        public virtual async Task RemoveAsync() =>
            await _cache.RemoveAsync(CacheKey);
    }
}
