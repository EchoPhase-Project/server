// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Configuration.Database;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

// --------------------------
// General
// --------------------------

namespace EchoPhase.Security.Cryptography.Vaults
{
    public sealed class SecretVault : SecretVaultBase, ISecretVault
    {
        public SecretVault(
            IConnectionMultiplexer redis,
            AesGcm aesGcm,
            IOptions<DatabaseOptions> settings,
            JsonSerializerOptions? jsonOptions = null)
            : base(redis, aesGcm, "secret_", settings, jsonOptions)
        {
        }
    }
}
