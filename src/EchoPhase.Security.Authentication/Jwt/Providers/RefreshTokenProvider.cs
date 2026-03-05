using System.Security.Cryptography;
using EchoPhase.Configuration.Authentication;
using EchoPhase.Configuration.Authentication.Refresh;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Types.Repository;
using Microsoft.Extensions.Options;
using EchoPhase.Identity;
using EchoPhase.Security.Authentication.Jwt.Claims;

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
                UserId       = user.Id,
                DeviceId     = deviceId,
                RefreshValue = GenerateSecureToken()
            };

            if (context is not null)
            {
                entry.Scopes = context.RequestedScopes
                    .Select(s => new RefreshTokenScope
                    {
                        RefreshTokenId = entry.Id,
                        Value          = s
                    }).ToList();

                entry.Intents = context.RequestedIntents
                    .Select(i => new RefreshTokenIntent
                    {
                        RefreshTokenId = entry.Id,
                        Value          = i
                    }).ToList();

                entry.Permissions = context.RequestedPermissions
                    .SelectMany(kvp => kvp.Value.Select(p => new RefreshTokenPermissionEntry
                    {
                        RefreshTokenId = entry.Id,
                        Resource       = kvp.Key,
                        Permission     = p
                    })).ToList();
            }

            _db.RefreshTokens.Add(entry);
            await _db.SaveChangesAsync();

            var jwt = await _jwtService.GenerateTokenAsync(user, _settings.Lifetime, context);
            return new TokenInitial
            {
                Id     = entry.Id,
                Tokens = new TokenPair
                {
                    AccessToken  = jwt,
                    RefreshToken = entry.RefreshValue
                }
            };
        }

        public async Task<TokenPair> RefreshAsync(
            Guid id,
            string refreshToken)
        {
            var existing = _repository.Get(x =>
            {
                x.Ids           = [id];
                x.RefreshValues = [refreshToken];
            }).Data.FirstOrDefault();

            if (existing is null)
                throw new UnauthorizedAccessException("Invalid refresh token.");

            var user = _userRepository.Get(opts =>
            {
                opts.Ids = [existing.UserId];
            }).Data.FirstOrDefault();

            if (user is null)
                throw new InvalidOperationException("Invalid UserId.");

            var context = new ClaimsEnrichmentContext
            {
                User                 = user,
                RequestedScopes      = existing.Scopes.Select(s => s.Value).ToList(),
                RequestedIntents     = existing.Intents.Select(i => i.Value).ToList(),
                RequestedPermissions = existing.Permissions
                    .GroupBy(p => p.Resource)
                    .ToDictionary(g => g.Key, g => g.Select(p => p.Permission).ToArray())
            };

            existing.RefreshValue = GenerateSecureToken();
            _db.RefreshTokens.Update(existing);
            await _db.SaveChangesAsync();

            var jwt = await _jwtService.GenerateTokenAsync(user, _settings.Lifetime, context);
            return new TokenPair
            {
                AccessToken  = jwt,
                RefreshToken = existing.RefreshValue
            };
        }

        public async Task RevokeAsync(Guid userId, Guid Id)
        {
            var existing = _repository.Get(x =>
            {
                x.Ids = [Id];
                x.UserIds = [userId];
            }).Data.FirstOrDefault();

            if (existing is not null)
            {
                _db.RefreshTokens.Remove(existing);
                await _db.SaveChangesAsync();
            }
        }

        public async Task RevokeAllAsync(Guid userId)
        {
            var tokens = _repository.Get(x => x.UserIds = [userId]).Data;
            _db.RefreshTokens.RemoveRange(tokens);
            await _db.SaveChangesAsync();
        }

        public Task<CursorPage<RefreshToken>> GetSessionsAsync(Guid userId, CursorOptions? cursor = null)
        {
            var tokens = _repository.Get(
                x => x.UserIds = [userId],
                cursor
            );
            return Task.FromResult(tokens);
        }
    }
}
