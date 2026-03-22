// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Types.Result;

namespace EchoPhase.Clients
{
    public class ExternalTokenService : IExternalTokenService
    {
        private readonly ExternalTokenRepository _repository;
        private readonly IClientTokenProviderRegistry _registry;
        private readonly IClientSecretVault _vault;

        public ExternalTokenService(
            ExternalTokenRepository repository,
            IClientTokenProviderRegistry registry,
            IClientSecretVault vault)
        {
            _repository = repository;
            _registry = registry;
            _vault = vault;
        }

        // ── Read ─────────────────────────────────────────────────────────────

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

        // ── Write ────────────────────────────────────────────────────────────

        public async Task<int> SetAsync(ExternalToken entity)
        {
            var cacheKey = $"{entity.ProviderName}:{entity.TokenName}";

            await _vault.DeleteAsync(entity.UserId.ToString(), cacheKey);

            return await _repository.Set(entity);
        }

        // ── Delete ───────────────────────────────────────────────────────────

        public async Task<bool> DeleteAsync(
            Guid userId,
            string providerName,
            string tokenName)
        {
            var token = _repository.Query()
                .WithUserIds(userId)
                .WithProviderNames(providerName)
                .WithTokenNames(tokenName)
                .FirstOrDefault();

            if (token is null)
                return false;

            _repository.Remove(token);
            await _repository.SaveAsync();

            // Best-effort cache eviction; a failure here is non-fatal because
            // the DB row is already gone – the cache entry will simply expire.
            await _vault.DeleteAsync(userId.ToString(), $"{providerName}:{tokenName}");

            return true;
        }

        public async Task DeleteAllAsync(Guid userId)
        {
            var tokens = _repository.Query()
                .WithUserIds(userId)
                .ToList();

            foreach (var token in tokens)
                _repository.Remove(token);

            await _repository.SaveAsync();

            // Atomic bulk eviction via Redis Set membership.
            await _vault.DeleteAllAsync(userId.ToString());
        }

        // ── Index ────────────────────────────────────────────────────────────

        public IEnumerable<string> GetKeyNames(Guid userId) =>
            _repository.Query()
                .WithUserIds(userId)
                .ToList()
                .Select(t => $"{t.ProviderName}:{t.TokenName}");
    }
}
