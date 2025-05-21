using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

using EchoPhase.Funcs;
using EchoPhase.Interfaces;

namespace EchoPhase.DAL.Redis
{
	public class RedisEntityEntry<T> : ICacheEntityEntry<T>
	{
		private readonly string _key;
		private readonly IDistributedCache _cache;

		public RedisEntityEntry(
				IDistributedCache cache,
				string key)
		{
			_cache = cache;
			_key = key;
		}

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
			string? cachedData = await _cache.GetStringAsync(_key);
			if (cachedData is null)
				throw new NullReferenceException($"Data not found in cache for key {_key}.");

			try
			{
				T? result = JsonSerializer.Deserialize<T>(cachedData);
				
				if (EqualityComparer<T>.Default.Equals(result, default(T))) 
					throw new InvalidOperationException($"Unable to deserialize result for key {_key}.");

				if (result is null)
					throw new NullReferenceException($"Data is null for key {_key}.");

				await _cache.RefreshAsync(_key);
				return result;
			}
			catch (JsonException ex)
			{
				throw new InvalidOperationException($"Error deserializing data for key {_key}: {ex.Message}.", ex);
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
			await _cache.SetStringAsync(_key, serializedData, new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = cacheDuration
			});
		}

		public virtual async Task<T> SetAsync(Func<T> getData, TimeSpan cacheDuration) =>
			await SetAsync(getData.ToAsync(), cacheDuration);

		public virtual async Task<T> GetOrSetAsync(Func<T> getData, TimeSpan cacheDuration) =>
			await GetOrSetAsync(getData.ToAsync(), cacheDuration);

		public virtual async Task RemoveAsync() =>
			await _cache.RemoveAsync(_key);
	}
}

