// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;
using System.Text;
using EchoPhase.Security.Cryptography.Vaults;

namespace EchoPhase.Security.Authentication.Secrets
{
    /// <summary>
    /// Hashes high-entropy secrets (refresh tokens, client secrets) with
    /// HMAC-SHA256 under a key kept in the key vault, outside the database.
    /// The secrets are 256-bit random values, so no salt or slow hash is needed
    /// and lookups by hash leak nothing useful through timing; the key also stops
    /// anyone with database write access from planting a secret of their choosing.
    /// Passwords are low-entropy and use Argon2 instead.
    /// </summary>
    public abstract class SecretHasher
    {
        private readonly KeyRing _keys;
        private readonly string _keyName;

        protected SecretHasher(KeyRing keys, string keyName)
        {
            _keys = keys;
            _keyName = keyName;
        }

        public string Hash(string secret)
            => Convert.ToHexString(HMACSHA256.HashData(_keys.Get(_keyName), Encoding.UTF8.GetBytes(secret)));

        /// <summary>
        /// Constant-time comparison against a stored hash; <c>false</c> for a missing hash.
        /// </summary>
        public bool Matches(string secret, string? expectedHash)
            => expectedHash is not null
               && CryptographicOperations.FixedTimeEquals(
                   Encoding.ASCII.GetBytes(Hash(secret)),
                   Encoding.ASCII.GetBytes(expectedHash));
    }
}
