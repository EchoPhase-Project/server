// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;
using StackExchange.Redis;

namespace EchoPhase.Clients
{
    public interface IClientSecretVault
    {
        // --------------------------
        // Exists
        // --------------------------

        Task<bool> ExistsAsync(string userId, string keyName);
        bool Exists(string userId, string keyName);

        // --------------------------
        // Get
        // --------------------------

        Task<IServiceResult<T>> GetAsync<T>(string userId, string keyName);
        IServiceResult<T> Get<T>(string userId, string keyName);

        // --------------------------
        // Set
        // --------------------------

        Task<bool> SetAsync<T>(
            string userId,
            string keyName,
            T value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None);

        bool Set<T>(
            string userId,
            string keyName,
            T value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None);

        // --------------------------
        // GetOrSet
        // --------------------------

        Task<IServiceResult<T>> GetOrSetAsync<T>(
            string userId,
            string keyName,
            Func<Task<T>>? generator = null,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            CommandFlags flags = CommandFlags.None);

        Task<IServiceResult<T>> GetOrSetAsync<T>(
            string userId,
            string keyName,
            Func<T> generator,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            CommandFlags flags = CommandFlags.None);

        // --------------------------
        // Delete
        // --------------------------

        Task<bool> DeleteAsync(string userId, string keyName);
        bool Delete(string userId, string keyName);
        Task DeleteAllAsync(string userId);

        // --------------------------
        // Index
        // --------------------------

        Task<IEnumerable<string>> GetUserKeyNamesAsync(string userId);
        Task<IDictionary<string, IServiceResult<T>>> GetAllAsync<T>(string userId);
    }
}
