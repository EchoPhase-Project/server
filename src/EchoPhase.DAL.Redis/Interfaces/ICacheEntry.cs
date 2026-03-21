// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Redis.Interfaces
{
    public interface ICacheEntry<T>
    {
        Task SetAsync(T data, TimeSpan cacheDuration);
        Task<T> SetAsync(Func<Task<T>> getData, TimeSpan cacheDuration);
        Task<T> SetAsync(Func<T> getData, TimeSpan cacheDuration);
        Task<T> GetAsync();
        Task<T> GetOrSetAsync(Func<Task<T>> getData, TimeSpan cacheDuration);
        Task<T> GetOrSetAsync(Func<T> getData, TimeSpan cacheDuration);
        Task RemoveAsync();
    }
}
