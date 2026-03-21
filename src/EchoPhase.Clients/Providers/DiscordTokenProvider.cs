using EchoPhase.Clients.Attributes;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Types.Result.Extensions;

namespace EchoPhase.Clients.Providers
{
    [ProviderName("Discord")]
    public class DiscordTokenProvider : IClientTokenProvider
    {
        private readonly ExternalTokenRepository _repository;
        private readonly ClientAccessProvider _cache;

        public DiscordTokenProvider(
            ExternalTokenRepository repository,
            ClientAccessProvider cache)
        {
            _repository = repository;
            _cache = cache;
        }

        public async Task<byte[]> ResolveAsync(Guid userId, string tokenName)
        {
            var result = await _cache.GetOrSetAsync(userId, "Discord", tokenName, async () =>
                _repository.Query()
                    .WithUserIds(userId)
                    .WithProviderNames("Discord")
                    .WithTokenNames(tokenName)
                    .FirstOrDefault()?.Value
                    ?? throw new KeyNotFoundException(
                        $"Token 'Discord:{tokenName}' not found for user '{userId}'."));

            return result.GetValueOrThrow();
        }
    }
}
