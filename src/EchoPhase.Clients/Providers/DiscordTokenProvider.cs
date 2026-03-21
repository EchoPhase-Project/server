// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Attributes;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Types.Result.Extensions;

namespace EchoPhase.Clients.Providers
{
    [ProviderName("Discord")]
    public class DiscordTokenProvider : IClientTokenProvider
    {
        private readonly ExternalTokenRepository _repository;
        private readonly ClientSecretVault _vault;

        public DiscordTokenProvider(
            ExternalTokenRepository repository,
            ClientSecretVault vault)
        {
            _repository = repository;
            _vault = vault;
        }

        public async Task<byte[]> ResolveAsync(Guid userId, string tokenName)
        {
            var result = await _vault.GetOrSetAsync(userId.ToString(), $"Discord:{tokenName}", async () =>
                _repository.Query()
                    .WithUserIds(userId)
                    .WithProviderNames("Discord")
                    .WithTokenNames(tokenName)
                    .FirstOrDefault()?.Value
                    ?? throw new KeyNotFoundException(
                        $"Token 'Discord:{tokenName}' not found for user '{userId}'."), TimeSpan.FromHours(4));

            return result.GetValueOrThrow();
        }
    }
}
