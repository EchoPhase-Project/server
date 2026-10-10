// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using System.Text.Json;
using EchoPhase.Connections.Models;
using EchoPhase.Security.Cryptography;

namespace EchoPhase.Connections.Services
{
    /// <summary>
    /// Encrypts token payloads and client secrets at rest.
    /// </summary>
    public class SecretProtector
    {
        private readonly AesGcm _aes;

        public SecretProtector(AesGcm aes)
        {
            _aes = aes;
        }

        public byte[] Protect(ConnectionSecret secret)
            => _aes.Encrypt(JsonSerializer.SerializeToUtf8Bytes(secret));

        /// <summary>
        /// Returns <c>null</c> for a cleared secret (connection awaiting re-authentication).
        /// </summary>
        public ConnectionSecret? UnprotectSecret(byte[] value)
        {
            if (value.Length == 0)
                return null;

            var secret = JsonSerializer.Deserialize<ConnectionSecret>(_aes.Decrypt(value));
            return string.IsNullOrEmpty(secret?.AccessToken) ? null : secret;
        }

        public byte[] Protect(string value) => _aes.Encrypt(Encoding.UTF8.GetBytes(value));

        public string? UnprotectString(byte[]? value)
            => value is { Length: > 0 } ? Encoding.UTF8.GetString(_aes.Decrypt(value)) : null;
    }
}
