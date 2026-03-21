// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;
using StackExchange.Redis;

namespace EchoPhase.Security.Cryptography.Vaults
{
    public interface ISecretVault
    {
        // --------------------------
        // Exists
        // --------------------------

        Task<bool> ExistsAsync(string key);
        bool Exists(string key);

        // --------------------------
        // Get
        // --------------------------

        Task<IServiceResult<T>> GetAsync<T>(string key);
        IServiceResult<T> Get<T>(string key);

        // --------------------------
        // Set
        // --------------------------

        Task<bool> SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None);

        bool Set<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None);

        // --------------------------
        // GetOrSet
        // --------------------------

        Task<IServiceResult<T>> GetOrSetAsync<T>(
            string key,
            Func<Task<T>>? generator = null,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            CommandFlags flags = CommandFlags.None);
        Task<IServiceResult<T>> GetOrSetAsync<T>(
            string key,
            Func<T> generator,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            CommandFlags flags = CommandFlags.None);
        IServiceResult<T> GetOrSet<T>(
            string key,
            Func<T>? generator = null,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            CommandFlags flags = CommandFlags.None);

        // --------------------------
        // Delete
        // --------------------------

        Task<bool> DeleteAsync(string key);
        bool Delete(string key);
    }
}
