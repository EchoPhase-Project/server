// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Text;
using EchoPhase.Configuration.Argon2;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Security.Cryptography.Vaults;
using Isopoh.Cryptography.Argon2;
using Isopoh.Cryptography.SecureArray;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RNG = System.Security.Cryptography.RandomNumberGenerator;

namespace EchoPhase.Security.Hashers
{
    /// <summary>
    /// Argon2id password hashes with a pepper (the key ring's Argon2 key) as
    /// Argon2's secret input. Hashing reuses one <see cref="Argon2Memory"/> per
    /// slot: the MemoryKiB working area is allocated once per slot, not per
    /// hash, so sign-ins no longer churn the large-object heap.
    /// </summary>
    public class Argon2Hasher : IPasswordHasher<User>
    {
        private readonly Argon2Options _settings;
        private readonly KeyRing _keys;
        private readonly SemaphoreSlim _slots;
        private readonly ConcurrentBag<Argon2Memory> _memories = new();

        public Argon2Hasher(
            IOptions<Argon2Options> settings,
            KeyRing keys
        )
        {
            _settings = settings.Value;
            _keys = keys;
            _slots = new SemaphoreSlim(_settings.MaxConcurrentHashes);
        }

        public string HashPassword(User user, string password)
        {
            var config = NewConfig(password);
            config.Type = Argon2Type.HybridAddressing;
            config.Version = Argon2Version.Nineteen;
            config.TimeCost = _settings.Iterations;
            config.MemoryCost = _settings.MemoryKiB;
            config.Lanes = _settings.Parallelism;
            config.Salt = GenerateSalt();

            return WithMemory(config, memory =>
            {
                using var argon2 = new Argon2(memory);
                return memory.EncodeString(argon2.Hash());
            });
        }

        public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
        {
            var config = NewConfig(providedPassword);

            SecureArray<byte>? expected = null;
            try
            {
                // Loads the parameters the hash was made with. It also sets
                // Threads to the lane count: back to one (see NewConfig).
                if (!config.DecodeString(hashedPassword, out expected) || expected == null)
                    return PasswordVerificationResult.Failed;
                config.Threads = 1;

                var matches = WithMemory(config, memory =>
                {
                    using var argon2 = new Argon2(memory);
                    return Argon2.FixedTimeEquals(expected.Buffer, argon2.Hash());
                });

                if (!matches)
                    return PasswordVerificationResult.Failed;

                // Identity re-hashes with the current parameters on this sign-in.
                return IsCurrent(config)
                    ? PasswordVerificationResult.Success
                    : PasswordVerificationResult.SuccessRehashNeeded;
            }
            finally
            {
                expected?.Dispose();
            }
        }

        private Argon2Config NewConfig(string password) => new()
        {
            HashLength = 32,
            // One thread per hash: lanes on extra threads would come from the
            // thread pool, which sign-ins already use; concurrent sign-ins,
            // not lanes, use the cores (PasswordHashingGate).
            Threads = 1,
            Secret = _keys.Get(_settings.Key),
            Password = Encoding.UTF8.GetBytes(password),
            // The memory outlives the call: do not leave the password or the
            // pepper in its buffers.
            ClearPassword = true,
            ClearSecret = true,
        };

        // Hard cap on hashes (and so memory) in flight. Callers queue in
        // PasswordHashingGate first, asynchronously, so this rarely waits; it
        // covers paths that hash without the gate (warm-up). A slot owns its
        // memory, so there are never more memories than slots.
        private T WithMemory<T>(Argon2Config config, Func<Argon2Memory, T> work)
        {
            _slots.Wait();
            Argon2Memory? memory = null;
            try
            {
                if (_memories.TryTake(out memory))
                    memory.Reset(config);
                else
                    memory = new Argon2Memory(config, Argon2MemoryPolicy.NoShrink, LockMemoryPolicy.BestEffort);

                return work(memory);
            }
            finally
            {
                if (memory != null)
                    _memories.Add(memory);
                _slots.Release();
            }
        }

        private bool IsCurrent(Argon2Config decoded)
            => decoded.Type == Argon2Type.HybridAddressing
               && decoded.Version == Argon2Version.Nineteen
               && decoded.TimeCost == _settings.Iterations
               && decoded.MemoryCost == _settings.MemoryKiB
               && decoded.Lanes == _settings.Parallelism;

        private static byte[] GenerateSalt()
        {
            var salt = new byte[16];
            RNG.Fill(salt);
            return salt;
        }
    }
}
