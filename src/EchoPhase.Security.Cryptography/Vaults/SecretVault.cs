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
        protected override string KeyPrefix => "secret_";

        public SecretVault(
            IConnectionMultiplexer redis,
            AesGcm aesGcm,
            IOptions<DatabaseOptions> settings,
            JsonSerializerOptions? jsonOptions = null)
            : base(redis, aesGcm, settings, jsonOptions) { }
    }
}
