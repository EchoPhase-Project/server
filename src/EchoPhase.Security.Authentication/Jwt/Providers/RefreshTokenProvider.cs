// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;
using EchoPhase.Configuration.Authentication;
using EchoPhase.Configuration.Authentication.Refresh;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;
using EchoPhase.Security.Authentication.Jwt.Claims;
using EchoPhase.Security.Authentication.Jwt.Exceptions;
using EchoPhase.Types.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoPhase.Security.Authentication.Jwt.Providers
{
    public class RefreshTokenProvider : IRefreshTokenProvider
    {
        private readonly RefreshTokenRepository _repository;
        private readonly PostgresContext _db;
        private readonly IJwtTokenProvider _jwtService;
        private readonly RefreshOptions _settings;
        private readonly IUserService _userService;
        private readonly UserRepository _userRepository;

        public RefreshTokenProvider(
            RefreshTokenRepository repository,
            PostgresContext db,
            IJwtTokenProvider jwtService,
            IOptions<AuthenticationOptions> settings,
            IUserService userService,
            UserRepository userRepository
        )
        {
            _repository = repository;
            _db = db;
            _jwtService = jwtService;
            _settings = settings.Value.Refresh;
            _userService = userService;
            _userRepository = userRepository;
        }

        private static string GenerateSecureToken(int size = 128)
        {
            var bytes = new byte[size];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public async Task<TokenInitial> CreateAsync(
            User user,
            string deviceId,
            ClaimsEnrichmentContext? context = null)
        {
            var entry = new RefreshToken
            {
                UserId = user.Id,
                DeviceId = deviceId,
                RefreshValue = GenerateSecureToken()
            };

            if (context is not null)
            {
                entry.Scopes = context.RequestedScopes
                    .Select(s => new RefreshTokenScope
                    {
                        RefreshTokenId = entry.Id,
                        Value = s
                    }).ToList();

                entry.Intents = context.RequestedIntents
                    .Select(i => new RefreshTokenIntent
                    {
                        RefreshTokenId = entry.Id,
                        Value = i
                    }).ToList();

                entry.Permissions = context.RequestedPermissions
                    .SelectMany(kvp => kvp.Value.Select(p => new RefreshTokenPermissionEntry
                    {
                        RefreshTokenId = entry.Id,
                        Resource = kvp.Key,
                        Permission = p
                    })).ToList();
            }

            _db.RefreshTokens.Add(entry);
            await _db.SaveChangesAsync();

            var jwt = await _jwtService.GenerateTokenAsync(user, _settings.Lifetime, context);
            return new TokenInitial
            {
                Id = entry.Id,
                Tokens = new TokenPair
                {
                    AccessToken = jwt,
                    RefreshToken = entry.RefreshValue
                }
            };
        }

        public async Task<TokenPair> RefreshAsync(Guid id, string refreshToken)
        {
            var existing = _repository.Query()
                .WithIds(id)
                .WithClaims()
                .WithAudits()
                .FirstOrDefault()
                ?? throw new UnauthorizedAccessException("Invalid refresh token.");

            var isReused = existing.Audits.Any(a => a.Token == refreshToken);
            if (isReused)
            {
                var userId = existing.UserId;
                _db.RefreshTokens.Remove(existing);
                await _db.SaveChangesAsync();
                throw new RefreshTokenReusedException(userId);
            }

            if (existing.RefreshValue != refreshToken)
                throw new UnauthorizedAccessException("Invalid refresh token.");

            var user = _userRepository.Query()
                .WithIds(existing.UserId)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("Invalid UserId.");

            var audit = new RefreshTokenAudit
            {
                RefreshTokenId = existing.Id,
                Token = refreshToken
            };
            _db.RefreshTokenAudits.Add(audit);

            var context = new ClaimsEnrichmentContext
            {
                User = user,
                RequestedScopes = existing.Scopes.Select(s => s.Value).ToList(),
                RequestedIntents = existing.Intents.Select(i => i.Value).ToList(),
                RequestedPermissions = existing.Permissions
                    .GroupBy(p => p.Resource)
                    .ToDictionary(g => g.Key, g => g.Select(p => p.Permission).ToArray())
            };

            existing.RefreshValue = GenerateSecureToken();
            _db.RefreshTokens.Update(existing);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new UnauthorizedAccessException("Refresh token already used. Please re-authenticate.");
            }

            var jwt = await _jwtService.GenerateTokenAsync(user, _settings.Lifetime, context);
            return new TokenPair
            {
                AccessToken = jwt,
                RefreshToken = existing.RefreshValue
            };
        }

        public async Task RevokeAsync(Guid userId, Guid id)
        {
            var existing = _repository.Query()
                .WithIds(id)
                .WithUserIds(userId)
                .FirstOrDefault();

            if (existing is not null)
            {
                _db.RefreshTokens.Remove(existing);
                await _db.SaveChangesAsync();
            }
        }

        public async Task RevokeAllAsync(Guid userId)
        {
            var tokens = _repository.Query()
                .WithUserIds(userId)
                .ToList();

            _db.RefreshTokens.RemoveRange(tokens);
            await _db.SaveChangesAsync();
        }

        public Task<CursorPage<RefreshToken>> GetSessionsAsync(Guid userId, CursorOptions? cursor = null)
        {
            var query = _repository.Query()
                .WithUserIds(userId)
                .Include(t => t.Intents)
                .Include(t => t.Scopes)
                .Include(t => t.Permissions);

            if (cursor is not null)
                query.WithCursor(cursor);

            return Task.FromResult(query.ToPage());
        }
    }
}
