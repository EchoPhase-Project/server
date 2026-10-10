// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Abstractions;
using EchoPhase.Configuration.Clients;
using EchoPhase.Connections.Locking;
using EchoPhase.Connections.Models;
using EchoPhase.Connections.Providers;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Types.Result;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EchoPhase.Connections.Services
{
    /// <remarks>
    /// Connections are always read without tracking and written back with
    /// <c>Update</c>, so the row's concurrency stamp guards against a refresh
    /// racing a background validation. Token refreshes for one connection are
    /// serialised by <see cref="IConnectionLock"/>.
    /// </remarks>
    public class ConnectionService : IConnectionService
    {
        private const int MaxErrorLength = 512;

        private readonly ConnectionRepository _repository;
        private readonly IOAuthAppService _apps;
        private readonly IConnectionProviderRegistry _providers;
        private readonly SecretProtector _protector;
        private readonly IConnectionLock _locks;
        private readonly IConnectionEvents _events;
        private readonly ClientsOptions _options;
        private readonly ILogger<ConnectionService> _logger;
        private readonly TimeProvider _time;

        public ConnectionService(
            ConnectionRepository repository,
            IOAuthAppService apps,
            IConnectionProviderRegistry providers,
            SecretProtector protector,
            IConnectionLock locks,
            IConnectionEvents events,
            IOptions<ClientsOptions> options,
            ILogger<ConnectionService> logger,
            TimeProvider time)
        {
            _repository = repository;
            _apps = apps;
            _providers = providers;
            _protector = protector;
            _locks = locks;
            _events = events;
            _options = options.Value;
            _logger = logger;
            _time = time;
        }

        // ── Read ─────────────────────────────────────────────────────────────

        public async Task<IReadOnlyList<ConnectionSummary>> ListAsync(
            Guid userId, string? provider = null, CancellationToken ct = default)
        {
            var query = _repository.Query().WithUserIds(userId);
            if (provider is not null)
                query.WithProviders(provider.ToLowerInvariant());

            var connections = await query
                .AsNoTracking()
                .ToListAsync(ct);

            return connections
                .OrderBy(c => c.Provider).ThenBy(c => c.Name)
                .Select(ConnectionSummary.From)
                .ToList();
        }

        // ── Write ────────────────────────────────────────────────────────────

        public async Task<IServiceResult<ConnectionSummary>> SaveManualAsync(
            Guid userId, string provider, ManualConnectionRequest request, CancellationToken ct = default)
        {
            var impl = _providers.Find(provider);
            if (impl is null)
                return Failure<ConnectionSummary>(ConnectionErrors.UnknownProvider, $"Unknown provider '{provider}'.");

            var validation = await impl.ValidateAsync(request.AccessToken, ct);
            switch (validation.Status)
            {
                case TokenValidationStatus.Invalid:
                    return Failure<ConnectionSummary>(ConnectionErrors.Invalid, $"The token was rejected: {validation.Error}");
                case TokenValidationStatus.Unavailable:
                    return Failure<ConnectionSummary>(ConnectionErrors.Upstream, $"Could not validate the token: {validation.Error}");
            }

            Guid? appId = null;
            string? refreshToken = null;

            if (impl.ManualAuthType == ConnectionAuthType.OAuth2)
            {
                var clientId = validation.Identity?.ClientId;
                if (string.IsNullOrEmpty(clientId))
                    return Failure<ConnectionSummary>(ConnectionErrors.Invalid, "Could not determine which app issued the token.");

                var match = await _apps.MatchManualAsync(userId, impl.Name, clientId, NullIfBlank(request.ClientSecret), ct);
                if (!match.Successful)
                    return ServiceResult<ConnectionSummary>.Failure(match.Error);

                appId = match.Value;
                refreshToken = NullIfBlank(request.RefreshToken);
            }

            return await UpsertAsync(
                userId, impl.Name, request.Name, impl.ManualAuthType, appId,
                new ConnectionSecret(request.AccessToken, refreshToken),
                validation.Identity, ct);
        }

        public Task<IServiceResult<ConnectionSummary>> SaveOAuthAsync(
            Guid userId, string provider, string name, Guid? appId,
            OAuthTokenSet tokens, TokenIdentity? identity, CancellationToken ct = default)
            => UpsertAsync(
                userId, provider.ToLowerInvariant(), name, ConnectionAuthType.OAuth2, appId,
                new ConnectionSecret(tokens.AccessToken, tokens.RefreshToken),
                identity ?? new TokenIdentity(null, null, null, tokens.Scopes, tokens.ExpiresIn),
                ct);

        public async Task<IServiceResult> DeleteAsync(Guid userId, Guid connectionId, CancellationToken ct = default)
        {
            var connection = await _repository.Query()
                .WithUserIds(userId)
                .WithIds(connectionId)
                .FirstOrDefaultAsync(ct);

            if (connection is null)
                return ServiceResult.Failure(e => e.Set(ConnectionErrors.NotFound, "Connection not found."));

            _repository.Remove(connection);
            await _repository.SaveAsync(ct);
            return ServiceResult.Success();
        }

        private async Task<IServiceResult<ConnectionSummary>> UpsertAsync(
            Guid userId, string provider, string name, ConnectionAuthType authType, Guid? appId,
            ConnectionSecret secret, TokenIdentity? identity, CancellationToken ct)
        {
            // Never interleave with a refresh of the same connection.
            await using var _ = await _locks.AcquireAsync(userId, provider, name, ct);

            var existing = await FindAsync(userId, provider, name, ct);
            var connection = existing ?? new Connection { UserId = userId, Provider = provider, Name = name };

            connection.AuthType = authType;
            connection.Status = ConnectionStatus.Active;
            connection.OAuthAppId = appId;
            connection.ExternalAccountId = identity?.ExternalAccountId;
            connection.ExternalLogin = identity?.Login;
            connection.Scopes = identity?.Scopes.ToArray() ?? [];
            connection.Secret = _protector.Protect(secret);
            connection.ExpiresAt = ExpiryFrom(identity?.ExpiresIn);
            connection.LastValidatedAt = identity is not null ? _time.GetUtcNow().UtcDateTime : null;
            connection.LastError = null;

            await PersistAsync(connection, isNew: existing is null, ct);
            return ServiceResult<ConnectionSummary>.Success(ConnectionSummary.From(connection));
        }

        // ── Credentials ──────────────────────────────────────────────────────

        public async Task<IServiceResult<ResolvedCredential>> ResolveAsync(
            ConnectionRef reference, string provider, CancellationToken ct = default)
        {
            var connection = await FindAsync(reference.UserId, provider, reference.Name, ct);
            if (connection is null)
                return NotFound(reference);

            if (connection.Status != ConnectionStatus.Active)
                return ReauthRequired(connection);

            var secret = _protector.UnprotectSecret(connection.Secret);
            if (secret is null)
                return ReauthRequired(connection);

            if (IsExpiring(connection) && secret.RefreshToken is not null)
                return await RefreshAsync(reference, provider, secret.AccessToken, validateFirst: false, ct);

            return await ToCredentialAsync(connection, secret, ct);
        }

        public Task<IServiceResult<ResolvedCredential>> RecoverAsync(
            ConnectionRef reference, string provider, string rejectedAccessToken, CancellationToken ct = default)
            => RefreshAsync(reference, provider, rejectedAccessToken, validateFirst: true, ct);

        private async Task<IServiceResult<ResolvedCredential>> RefreshAsync(
            ConnectionRef reference, string provider, string rejectedAccessToken, bool validateFirst,
            CancellationToken ct)
        {
            await using var _ = await _locks.AcquireAsync(reference.UserId, provider, reference.Name, ct);

            // Re-read under the lock: another request or instance may have
            // refreshed (or invalidated) the connection while we waited.
            var connection = await FindAsync(reference.UserId, provider, reference.Name, ct);
            if (connection is null)
                return NotFound(reference);

            if (connection.Status != ConnectionStatus.Active)
                return ReauthRequired(connection);

            var secret = _protector.UnprotectSecret(connection.Secret);
            if (secret is null)
                return ReauthRequired(connection);

            if (secret.AccessToken != rejectedAccessToken)
                return await ToCredentialAsync(connection, secret, ct);

            if (connection.AuthType != ConnectionAuthType.OAuth2
                || _providers.Find(provider) is not IOAuthConnectionProvider oauth)
                return await MarkReauthRequiredAsync(connection, "The provider rejected the token.", ct);

            if (validateFirst)
            {
                // Twitch answers 401 for a missing scope too; only a token the
                // identity server also rejects is worth refreshing.
                var validation = await oauth.ValidateAsync(secret.AccessToken, ct);
                if (validation.Status == TokenValidationStatus.Valid)
                    return Failure<ResolvedCredential>(ConnectionErrors.Forbidden,
                        "The token is valid but the provider refused the request; check the connection's scopes.");

                if (validation.Status == TokenValidationStatus.Unavailable)
                    return Failure<ResolvedCredential>(ConnectionErrors.Upstream,
                        $"Could not validate the token: {validation.Error}");
            }

            if (string.IsNullOrEmpty(secret.RefreshToken))
                return await MarkReauthRequiredAsync(connection,
                    "The access token is no longer valid and no refresh token is stored.", ct);

            var client = await _apps.ResolveClientAsync(provider, connection.OAuthAppId, ct);
            if (!client.Successful)
                return ServiceResult<ResolvedCredential>.Failure(client.Error);

            var grant = await oauth.RefreshAsync(client.Value!, secret.RefreshToken, ct);

            switch (grant.Status)
            {
                case TokenGrantStatus.Rejected:
                    return await MarkReauthRequiredAsync(connection, $"The refresh token was rejected: {grant.Error}", ct);

                case TokenGrantStatus.Unavailable:
                    // Transient: keep the tokens, just record what happened.
                    connection.LastError = Truncate($"Token refresh failed: {grant.Error}");
                    await PersistAsync(connection, isNew: false, ct);
                    return Failure<ResolvedCredential>(ConnectionErrors.Upstream, connection.LastError);
            }

            var tokens = grant.Tokens!;
            var next = new ConnectionSecret(tokens.AccessToken, tokens.RefreshToken ?? secret.RefreshToken);

            connection.Secret = _protector.Protect(next);
            connection.ExpiresAt = ExpiryFrom(tokens.ExpiresIn);
            if (tokens.Scopes.Count > 0)
                connection.Scopes = tokens.Scopes.ToArray();
            connection.LastValidatedAt = _time.GetUtcNow().UtcDateTime;
            connection.LastError = null;

            await PersistAsync(connection, isNew: false, ct);

            _logger.LogInformation(
                "Refreshed {Provider} connection {ConnectionId} for user {UserId}.",
                provider, connection.Id, connection.UserId);

            return ServiceResult<ResolvedCredential>.Success(new ResolvedCredential(
                connection.Id, connection.Provider, connection.AuthType, next.AccessToken, client.Value!.ClientId));
        }

        private async Task<IServiceResult<ResolvedCredential>> ToCredentialAsync(
            Connection connection, ConnectionSecret secret, CancellationToken ct)
        {
            string? clientId = null;

            if (connection.AuthType == ConnectionAuthType.OAuth2)
            {
                var client = await _apps.ResolveClientAsync(connection.Provider, connection.OAuthAppId, ct);
                if (!client.Successful)
                    return ServiceResult<ResolvedCredential>.Failure(client.Error);

                clientId = client.Value!.ClientId;
            }

            return ServiceResult<ResolvedCredential>.Success(new ResolvedCredential(
                connection.Id, connection.Provider, connection.AuthType, secret.AccessToken, clientId));
        }

        /// <summary>
        /// The tokens are dead: clear them and keep the row so the user can see
        /// which connection needs reconnecting (reconnecting keeps its id).
        /// </summary>
        private async Task<IServiceResult<ResolvedCredential>> MarkReauthRequiredAsync(
            Connection connection, string reason, CancellationToken ct)
        {
            connection.Status = ConnectionStatus.ReauthRequired;
            connection.Secret = [];
            connection.ExpiresAt = null;
            connection.LastError = Truncate(reason);

            await PersistAsync(connection, isNew: false, ct);

            _logger.LogWarning(
                "{Provider} connection {ConnectionId} for user {UserId} requires re-authentication: {Reason}",
                connection.Provider, connection.Id, connection.UserId, reason);

            try
            {
                await _events.ReauthRequiredAsync(connection.UserId, ConnectionSummary.From(connection), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to notify user {UserId} about connection {ConnectionId}.",
                    connection.UserId, connection.Id);
            }

            return ReauthRequired(connection);
        }

        // ── Background validation ────────────────────────────────────────────

        public async Task ValidateAllAsync(CancellationToken ct = default)
        {
            var connections = await _repository.Query()
                .WithAuthTypes(ConnectionAuthType.OAuth2)
                .WithStatuses(ConnectionStatus.Active)
                .AsNoTracking()
                .ToListAsync(ct);

            foreach (var connection in connections)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await ValidateOneAsync(connection, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Includes concurrency conflicts with a request that refreshed
                    // the same connection meanwhile; the next run will catch up.
                    _logger.LogWarning(ex, "Validation of connection {ConnectionId} failed.", connection.Id);
                }
            }
        }

        private async Task ValidateOneAsync(Connection connection, CancellationToken ct)
        {
            var secret = _protector.UnprotectSecret(connection.Secret);
            if (secret is null)
                return;

            var reference = new ConnectionRef(connection.UserId, connection.Name);

            if (IsExpiring(connection))
            {
                await RefreshAsync(reference, connection.Provider, secret.AccessToken, validateFirst: false, ct);
                return;
            }

            if (_providers.Find(connection.Provider) is not IOAuthConnectionProvider oauth)
                return;

            var validation = await oauth.ValidateAsync(secret.AccessToken, ct);

            switch (validation.Status)
            {
                case TokenValidationStatus.Valid:
                    connection.LastValidatedAt = _time.GetUtcNow().UtcDateTime;
                    connection.ExpiresAt = ExpiryFrom(validation.Identity?.ExpiresIn) ?? connection.ExpiresAt;
                    connection.LastError = null;
                    await PersistAsync(connection, isNew: false, ct);
                    break;

                case TokenValidationStatus.Invalid:
                    await RefreshAsync(reference, connection.Provider, secret.AccessToken, validateFirst: false, ct);
                    break;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private Task<Connection?> FindAsync(Guid userId, string provider, string name, CancellationToken ct)
            => _repository.Query()
                .WithUserIds(userId)
                .WithProviders(provider.ToLowerInvariant())
                .WithNames(name)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

        private async Task PersistAsync(Connection connection, bool isNew, CancellationToken ct)
        {
            if (isNew)
                _repository.Add(connection);
            else
                _repository.Update(connection);

            try
            {
                await _repository.SaveAsync(ct);
            }
            finally
            {
                _repository.Detach(connection);
            }
        }

        private bool IsExpiring(Connection connection)
            => connection.AuthType == ConnectionAuthType.OAuth2
               && connection.ExpiresAt is { } expiresAt
               && expiresAt - _options.RefreshSkew <= _time.GetUtcNow().UtcDateTime;

        private DateTime? ExpiryFrom(int? seconds)
            => seconds is > 0 ? _time.GetUtcNow().UtcDateTime.AddSeconds(seconds.Value) : null;

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value;

        private static string Truncate(string value)
            => value.Length <= MaxErrorLength ? value : value[..MaxErrorLength];

        private static IServiceResult<T> Failure<T>(string code, string message)
            => ServiceResult<T>.Failure(e => e.Set(code, message));

        private static IServiceResult<ResolvedCredential> NotFound(ConnectionRef reference)
            => Failure<ResolvedCredential>(ConnectionErrors.NotFound, $"Connection '{reference.Name}' not found.");

        private static IServiceResult<ResolvedCredential> ReauthRequired(Connection connection)
            => Failure<ResolvedCredential>(ConnectionErrors.ReauthRequired,
                $"Connection '{connection.Name}' must be reconnected: {connection.LastError ?? "credentials missing"}.");
    }
}
