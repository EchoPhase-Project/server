// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;

namespace EchoPhase.Clients.Providers
{
    public class ClientAccessProvider
    {
        private readonly IClientSecretVault _vault;

        public ClientAccessProvider(IClientSecretVault vault)
        {
            _vault = vault;
        }

        private static string CacheKey(string providerName, string tokenName) =>
            $"{providerName}:{tokenName}";

        public Task<IServiceResult<byte[]>> GetOrSetAsync(
            Guid userId,
            string providerName,
            string tokenName,
            Func<Task<byte[]>> factory)
            => _vault.GetOrSetAsync(
                userId.ToString(),
                CacheKey(providerName, tokenName),
                factory);

        public Task<bool> InvalidateAsync(Guid userId, string providerName, string tokenName)
            => _vault.DeleteAsync(
                userId.ToString(),
                CacheKey(providerName, tokenName));
    }
}
