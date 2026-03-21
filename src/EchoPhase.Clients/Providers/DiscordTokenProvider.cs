// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Types.Result.Extensions;

namespace EchoPhase.Clients.Providers
{
    public class DiscordTokenProvider : IClientTokenProvider
    {
        public static string ProviderName = "discord";

        private readonly ExternalTokenRepository _repository;
        private readonly IClientSecretVault _vault;

        public DiscordTokenProvider(
            ExternalTokenRepository repository,
            IClientSecretVault vault)
        {
            _repository = repository;
            _vault = vault;
        }

        public async Task<byte[]> ResolveAsync(Guid userId, string tokenName)
        {
            var result = await _vault.GetOrSetAsync(userId.ToString(), $"{ProviderName}:{tokenName}", async () =>
                _repository.Query()
                    .WithUserIds(userId)
                    .WithProviderNames(ProviderName)
                    .WithTokenNames(tokenName)
                    .FirstOrDefault()?.Value
                    ?? throw new KeyNotFoundException(
                        $"Token '{ProviderName}:{tokenName}' not found for user '{userId}'."), TimeSpan.FromHours(4));

            return result.GetValueOrThrow();
        }
    }
}
