using EchoPhase.Clients.Providers;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Types.Result;

namespace EchoPhase.Clients
{
    public class ExternalTokenService : IExternalTokenService
    {
        private readonly ExternalTokenRepository _repository;
        private readonly IClientTokenProviderRegistry _registry;
        private readonly ClientAccessProvider _cache;

        public ExternalTokenService(
            ExternalTokenRepository repository,
            IClientTokenProviderRegistry registry,
            ClientAccessProvider cache)
        {
            _repository = repository;
            _registry = registry;
            _cache = cache;
        }

        public async Task<IServiceResult<byte[]>> GetAsync(
            Guid userId,
            string providerName,
            string tokenName)
        {
            try
            {
                var provider = _registry.Get(providerName);
                var token = await provider.ResolveAsync(userId, tokenName);
                return ServiceResult<byte[]>.Success(token);
            }
            catch (Exception ex)
            {
                return ServiceResult<byte[]>.Failure(e => e.Set(
                    ex.GetType().Name.Replace("Exception", string.Empty),
                    ex.Message));
            }
        }

        public async Task<int> SetAsync(ExternalToken entity)
        {
            var result = await _repository.Set(entity);
            await _cache.InvalidateAsync(entity.UserId, entity.ProviderName, entity.TokenName);
            return result;
        }

        public async Task<bool> DeleteAsync(Guid userId, string providerName, string tokenName)
        {
            var token = _repository.Query()
                .WithUserIds(userId)
                .WithProviderNames(providerName)
                .WithTokenNames(tokenName)
                .FirstOrDefault();

            if (token is null) return false;

            _repository.Remove(token);
            await _repository.SaveAsync();
            await _cache.InvalidateAsync(userId, providerName, tokenName);
            return true;
        }

        public async Task DeleteAllAsync(Guid userId)
        {
            var tokens = _repository.Query()
                .WithUserIds(userId)
                .ToList();

            foreach (var token in tokens)
            {
                _repository.Remove(token);
                await _cache.InvalidateAsync(userId, token.ProviderName, token.TokenName);
            }

            await _repository.SaveAsync();
        }

        public IEnumerable<string> GetKeyNames(Guid userId)
        {
            return _repository.Query()
                .WithUserIds(userId)
                .ToList()
                .Select(t => $"{t.ProviderName}:{t.TokenName}");
        }
    }
}
