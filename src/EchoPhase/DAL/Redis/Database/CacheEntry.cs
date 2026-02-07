using System.Text.Json;
using EchoPhase.DAL.Redis.Interfaces;
using EchoPhase.Funcs;
using Microsoft.Extensions.Caching.Distributed;

namespace EchoPhase.DAL.Redis
{
    public class CacheEntry<T> : ICacheEntry<T>
    {
        private readonly string _cacheKey;
        private readonly Guid _tenantId;
        private readonly string _key;
        private readonly IDistributedCache _cache;

        public CacheEntry(
            IDistributedCache cache,
            string cacheKey,
            Guid tenantId,
            string key)
        {
            _cache = cache;
            _cacheKey = cacheKey;
            _tenantId = tenantId;
            _key = key;
        }

        public Guid TenantId => _tenantId;
        public string Key => _key;
        public string CacheKey => _cacheKey;

        public virtual async Task<T> GetOrSetAsync(Func<Task<T>> getData, TimeSpan cacheDuration)
        {
            try
            {
                T result = await GetAsync();
                return result;
            }
            catch (NullReferenceException)
            {
                return await SetAsync(getData, cacheDuration);
            }
            catch (InvalidOperationException)
            {
                await RemoveAsync();
                return await SetAsync(getData, cacheDuration);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public virtual async Task<T> GetAsync()
        {
            string? cachedData = await _cache.GetStringAsync(_cacheKey);
            if (cachedData is null)
                throw new NullReferenceException($"Data not found in cache for tenant {_tenantId}, key {_key} (cache key: {_cacheKey}).");
            try
            {
                T? result = JsonSerializer.Deserialize<T>(cachedData);
                if (EqualityComparer<T>.Default.Equals(result, default(T)))
                    throw new InvalidOperationException($"Unable to deserialize result for tenant {_tenantId}, key {_key}.");
                if (result is null)
                    throw new NullReferenceException($"Data is null for tenant {_tenantId}, key {_key}.");
                await _cache.RefreshAsync(_cacheKey);
                return result;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Error deserializing data for tenant {_tenantId}, key {_key}: {ex.Message}.", ex);
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
            await _cache.SetStringAsync(_cacheKey, serializedData, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheDuration
            });
        }

        public virtual async Task<T> SetAsync(Func<T> getData, TimeSpan cacheDuration) =>
            await SetAsync(getData.ToAsync(), cacheDuration);

        public virtual async Task<T> GetOrSetAsync(Func<T> getData, TimeSpan cacheDuration) =>
            await GetOrSetAsync(getData.ToAsync(), cacheDuration);

        public virtual async Task RemoveAsync() =>
            await _cache.RemoveAsync(_cacheKey);
    }
}
