// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;
using EchoPhase.Configuration.Authentication;
using EchoPhase.Configuration.Authentication.ApiClients;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;
using EchoPhase.Security.Authentication.Jwt.Claims;
using EchoPhase.Security.Authentication.Jwt.Providers;
using EchoPhase.Security.Authentication.Secrets;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoPhase.Security.Authentication.ApiClients
{
    public class ApiClientService : IApiClientService
    {
        private const int SecretBytes = 32;
        private const int MaxNameLength = 64;

        private readonly ApiClientRepository _repository;
        private readonly UserRepository _users;
        private readonly IUserPermissionService _permissions;
        private readonly IJwtTokenProvider _jwt;
        private readonly ApiClientSecretHasher _hasher;
        private readonly ApiClientOptions _settings;
        private readonly TimeProvider _time;

        public ApiClientService(
            ApiClientRepository repository,
            UserRepository users,
            IUserPermissionService permissions,
            IJwtTokenProvider jwt,
            ApiClientSecretHasher hasher,
            IOptions<AuthenticationOptions> settings,
            TimeProvider time)
        {
            _repository = repository;
            _users = users;
            _permissions = permissions;
            _jwt = jwt;
            _hasher = hasher;
            _settings = settings.Value.ApiClients;
            _time = time;
        }

        private static string GenerateSecret()
            => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));

        public async Task<IReadOnlyList<ApiClient>> ListAsync(Guid userId, CancellationToken ct = default)
        {
            return await _repository.Query()
                .WithUserIds(userId)
                .WithPermissions()
                .OrderBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public async Task<IServiceResult<ApiClientCreated>> CreateAsync(
            Guid userId, string name, ApiClientGrant grant, DateTimeOffset? expiresAt, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
                return Failure<ApiClientCreated>(ApiClientErrors.Invalid, $"Name must be 1-{MaxNameLength} characters.");

            if (expiresAt is { } at && at <= _time.GetUtcNow())
                return Failure<ApiClientCreated>(ApiClientErrors.Invalid, "ExpiresAt must be in the future.");

            var owner = await _users.Query().WithIds(userId).FirstOrDefaultAsync(ct);
            if (owner is null)
                return Failure<ApiClientCreated>(ApiClientErrors.NotFound, "User not found.");

            // Fail early with a clear message; tokens are narrowed to the
            // ceiling again on every exchange in case roles change later.
            var ceiling = await _permissions.GetCeilingAsync(owner, ct);
            var exceeding = RolePermissions.Exceeding(ceiling, grant.Permissions);
            if (exceeding.Count > 0)
                return Failure<ApiClientCreated>(ApiClientErrors.Invalid,
                    $"Not allowed by your permissions: {string.Join(", ", exceeding)}.");

            var secret = GenerateSecret();
            var client = new ApiClient
            {
                UserId = userId,
                Name = name.Trim(),
                SecretHash = _hasher.Hash(secret),
                ExpiresAt = expiresAt?.UtcDateTime
            };
            client.Permissions = grant.Permissions
                .Select(kvp => new ApiClientPermissionEntry
                {
                    ApiClientId = client.Id,
                    Resource = kvp.Key,
                    Permissions = kvp.Value
                })
                .ToList();

            _repository.Add(client);
            await _repository.SaveAsync(ct);

            return ServiceResult<ApiClientCreated>.Success(new ApiClientCreated(client, secret));
        }

        public async Task<IServiceResult<string>> RotateSecretAsync(Guid userId, Guid clientId, CancellationToken ct = default)
        {
            var client = await FindOwnedAsync(userId, clientId, ct);
            if (client is null)
                return Failure<string>(ApiClientErrors.NotFound, "API client not found.");

            var secret = GenerateSecret();
            client.PreviousSecretHash = client.SecretHash;
            client.SecretHash = _hasher.Hash(secret);
            client.SecretRotatedAt = _time.GetUtcNow().UtcDateTime;

            await _repository.SaveAsync(ct);
            return ServiceResult<string>.Success(secret);
        }

        public async Task<IServiceResult> RevokePreviousSecretAsync(Guid userId, Guid clientId, CancellationToken ct = default)
        {
            var client = await FindOwnedAsync(userId, clientId, ct);
            if (client is null)
                return ServiceResult.Failure(e => e.Set(ApiClientErrors.NotFound, "API client not found."));

            client.PreviousSecretHash = null;
            await _repository.SaveAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<IServiceResult> DeleteAsync(Guid userId, Guid clientId, CancellationToken ct = default)
        {
            var client = await FindOwnedAsync(userId, clientId, ct);
            if (client is null)
                return ServiceResult.Failure(e => e.Set(ApiClientErrors.NotFound, "API client not found."));

            _repository.Remove(client);
            await _repository.SaveAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<IServiceResult<ClientAccessToken>> IssueTokenAsync(Guid clientId, string clientSecret, CancellationToken ct = default)
        {
            var client = await _repository.Query()
                .WithIds(clientId)
                .WithPermissions()
                .FirstOrDefaultAsync(ct);

            if (client is null
                || IsExpired(client)
                || !(_hasher.Matches(clientSecret, client.SecretHash)
                     || _hasher.Matches(clientSecret, client.PreviousSecretHash)))
                return InvalidClient();

            var user = await _users.Query()
                .WithIds(client.UserId)
                .FirstOrDefaultAsync(ct);

            if (user is null)
                return InvalidClient();

            var token = await _jwt.GenerateTokenAsync(user, _settings.TokenLifetime, new ClaimsEnrichmentContext
            {
                User = user,
                ClientId = client.Id,
                RequestedPermissions = client.Permissions.ToDictionary(p => p.Resource, p => p.Permissions)
            });

            client.LastUsedAt = _time.GetUtcNow().UtcDateTime;
            try
            {
                await _repository.SaveAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another instance exchanged the same credentials at the same
                // moment; its LastUsedAt is just as good.
            }

            return ServiceResult<ClientAccessToken>.Success(
                new ClientAccessToken(token, (int)_settings.TokenLifetime.TotalSeconds));
        }

        private Task<ApiClient?> FindOwnedAsync(Guid userId, Guid clientId, CancellationToken ct)
            => _repository.Query()
                .WithIds(clientId)
                .WithUserIds(userId)
                .FirstOrDefaultAsync(ct);

        private bool IsExpired(ApiClient client)
            => client.ExpiresAt is { } at && at <= _time.GetUtcNow().UtcDateTime;

        private static IServiceResult<ClientAccessToken> InvalidClient()
            => Failure<ClientAccessToken>(ApiClientErrors.InvalidClient, "Invalid client credentials.");

        private static IServiceResult<T> Failure<T>(string code, string message)
            => ServiceResult<T>.Failure(e => e.Set(code, message));
    }
}
