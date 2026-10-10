// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Argon2;
using Microsoft.Extensions.Options;

namespace EchoPhase.Security.Hashers
{
    /// <summary>
    /// Asynchronous queue in front of password hashing. IPasswordHasher is
    /// synchronous, so waiting inside it parks thread-pool threads; under a
    /// burst of sign-ins that starved the pool, and finished hashes could not
    /// resume their async work until every queued one was done. Waiting here
    /// releases the thread instead. Wrap each operation that hashes (sign-in,
    /// registration, password checks); one operation may hash twice (verify
    /// then re-hash) within its slot.
    /// </summary>
    public sealed class PasswordHashingGate
    {
        private readonly SemaphoreSlim _slots;

        public PasswordHashingGate(IOptions<Argon2Options> options)
        {
            _slots = new SemaphoreSlim(options.Value.MaxConcurrentHashes);
        }

        public async Task<T> RunAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
        {
            await _slots.WaitAsync(ct);
            try
            {
                return await operation();
            }
            finally
            {
                _slots.Release();
            }
        }
    }
}
