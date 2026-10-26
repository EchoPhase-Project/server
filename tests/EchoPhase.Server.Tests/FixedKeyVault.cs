// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Cryptography.Vaults;
using EchoPhase.Types.Result;
using StackExchange.Redis;

namespace EchoPhase.Server.Tests
{
    /// <summary>A vault holding one known key (bytes 1..32), for encryption in tests.</summary>
    internal sealed class FixedKeyVault : IKeyVault
    {
        public static readonly byte[] Pepper = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

        public IServiceResult<byte[]> GetOrSet(string key, Func<byte[]>? generator = null)
            => ServiceResult<byte[]>.Success(Pepper);

        public IServiceResult<byte[]> Get(string key) => GetOrSet(key);
        public bool Exists(string key) => true;
        public bool Set(string key, byte[] value, TimeSpan? expiry = null, bool keepTtl = false,
            When when = When.Always, CommandFlags flags = CommandFlags.None) => throw new NotSupportedException();

        public Task<bool> ExistsAsync(string key) => Task.FromResult(true);
        public Task<IServiceResult<byte[]>> GetAsync(string key) => Task.FromResult(GetOrSet(key));
        public Task<bool> SetAsync(string key, byte[] value, TimeSpan? expiry = null, bool keepTtl = false,
            When when = When.Always, CommandFlags flags = CommandFlags.None) => throw new NotSupportedException();
        public Task<IServiceResult<byte[]>> GetOrSetAsync(string key, Func<Task<byte[]>>? generator = null)
            => Task.FromResult(GetOrSet(key));
        public Task<IServiceResult<byte[]>> GetOrSetAsync(string key, Func<byte[]> syncGenerator)
            => Task.FromResult(GetOrSet(key));
    }
}
