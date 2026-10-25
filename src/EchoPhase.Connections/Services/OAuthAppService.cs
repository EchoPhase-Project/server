// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Connections.Models;
using EchoPhase.Connections.Providers;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Types.Result;

namespace EchoPhase.Connections.Services
{
    public class OAuthAppService : IOAuthAppService
    {
        private readonly OAuthAppRepository _apps;
        private readonly ConnectionRepository _connections;
        private readonly IConnectionProviderRegistry _providers;
        private readonly SecretProtector _protector;

        public OAuthAppService(
            OAuthAppRepository apps,
            ConnectionRepository connections,
            IConnectionProviderRegistry providers,
            SecretProtector protector)
        {
            _apps = apps;
            _connections = connections;
            _providers = providers;
            _protector = protector;
        }

        public async Task<IReadOnlyList<OAuthAppInfo>> ListAsync(Guid userId, CancellationToken ct = default)
        {
            var apps = await _apps.Query()
                .WithUserIds(userId)
                .AsNoTracking()
                .ToListAsync(ct);

            return apps
                .OrderBy(a => a.Provider).ThenBy(a => a.Name)
                .Select(OAuthAppInfo.From)
                .ToList();
        }

        public async Task<IServiceResult<OAuthAppInfo>> SaveAsync(
            Guid userId, string provider, string name, string clientId, string? clientSecret,
            CancellationToken ct = default)
        {
            if (_providers.Find(provider) is not IOAuthConnectionProvider oauth)
                return ServiceResult<OAuthAppInfo>.Failure(e => e.Set(
                    ConnectionErrors.UnknownProvider, $"Provider '{provider}' does not support OAuth."));

            var app = await _apps.Query()
                .WithUserIds(userId)
                .WithProviders(oauth.Name)
                .WithClientIds(clientId)
                .FirstOrDefaultAsync(ct);

            if (app is null)
            {
                app = new OAuthApp { UserId = userId, Provider = oauth.Name, ClientId = clientId };
                _apps.Add(app);
            }

            app.Name = name;
            if (!string.IsNullOrEmpty(clientSecret))
                app.ClientSecret = _protector.Protect(clientSecret);

            await _apps.SaveAsync(ct);
            return ServiceResult<OAuthAppInfo>.Success(OAuthAppInfo.From(app));
        }

        public async Task<IServiceResult> DeleteAsync(Guid userId, Guid appId, CancellationToken ct = default)
        {
            var app = await _apps.Query()
                .WithUserIds(userId)
                .WithIds(appId)
                .FirstOrDefaultAsync(ct);

            if (app is null)
                return ServiceResult.Failure(e => e.Set(ConnectionErrors.NotFound, "OAuth app not found."));

            if (await _connections.Query().WithOAuthAppIds(appId).AnyAsync(ct))
                return ServiceResult.Failure(e => e.Set(
                    ConnectionErrors.Conflict, "OAuth app is still used by connections; delete them first."));

            _apps.Remove(app);
            await _apps.SaveAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<IServiceResult<OAuthClient>> ResolveClientAsync(
            string provider, Guid? appId, CancellationToken ct = default)
        {
            if (_providers.Find(provider) is not IOAuthConnectionProvider oauth)
                return Fail(ConnectionErrors.UnknownProvider, $"Provider '{provider}' does not support OAuth.");

            if (appId is null)
            {
                var platform = oauth.PlatformApp;
                return platform.IsConfigured
                    ? ServiceResult<OAuthClient>.Success(
                        new OAuthClient(null, platform.ClientId, platform.ClientSecret))
                    : Fail(ConnectionErrors.NotConfigured, $"The platform {oauth.Name} app is not configured.");
            }

            var app = await _apps.Query()
                .WithIds(appId.Value)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            return app is null
                ? Fail(ConnectionErrors.NotFound, "OAuth app not found.")
                : ServiceResult<OAuthClient>.Success(
                    new OAuthClient(app.Id, app.ClientId, _protector.UnprotectString(app.ClientSecret)));
        }

        public async Task<IServiceResult<Guid?>> MatchManualAsync(
            Guid userId, string provider, string clientId, string? clientSecret,
            CancellationToken ct = default)
        {
            if (_providers.Find(provider) is not IOAuthConnectionProvider oauth)
                return ServiceResult<Guid?>.Failure(e => e.Set(
                    ConnectionErrors.UnknownProvider, $"Provider '{provider}' does not support OAuth."));

            // A token issued to the platform app needs no app of its own.
            if (string.IsNullOrEmpty(clientSecret)
                && oauth.PlatformApp.IsConfigured
                && oauth.PlatformApp.ClientId == clientId)
                return ServiceResult<Guid?>.Success(null);

            var existing = await _apps.Query()
                .WithUserIds(userId)
                .WithProviders(oauth.Name)
                .WithClientIds(clientId)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            if (existing is not null && string.IsNullOrEmpty(clientSecret))
                return ServiceResult<Guid?>.Success(existing.Id);

            // Without a secret this is treated as a public client, which Twitch
            // lets refresh with the client id alone.
            var saved = await SaveAsync(userId, oauth.Name, existing?.Name ?? clientId, clientId, clientSecret, ct);
            return saved.Successful
                ? ServiceResult<Guid?>.Success(saved.Value.Id)
                : ServiceResult<Guid?>.Failure(saved.Error);
        }

        public Task<bool> IsOwnedAsync(Guid userId, string provider, Guid appId, CancellationToken ct = default)
            => _apps.Query()
                .WithUserIds(userId)
                .WithProviders(provider)
                .WithIds(appId)
                .AnyAsync(ct);

        private static IServiceResult<OAuthClient> Fail(string code, string message)
            => ServiceResult<OAuthClient>.Failure(e => e.Set(code, message));
    }
}
