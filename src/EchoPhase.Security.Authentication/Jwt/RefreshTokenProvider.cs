using System.Security.Cryptography;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Types.Repository;

namespace EchoPhase.Security.Authentication.Jwt
{
    public class RefreshTokenProvider : IRefreshTokenProvider
    {
        private readonly IRefreshTokenRepository _repository;
        private readonly PostgresContext _db;
        private readonly IJwtTokenProvider _jwtService;

        public RefreshTokenProvider(
            IRefreshTokenRepository repository,
            PostgresContext db,
            IJwtTokenProvider jwtService
        )
        {
            _repository = repository;
            _db = db;
            _jwtService = jwtService;
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

        public async Task<TokenPair> CreateAsync(User user, string deviceId)
        {
            var entry = new RefreshToken
            {
                UserId = user.Id,
                DeviceId = deviceId,
                RefreshValue = GenerateSecureToken()
            };
            _db.RefreshTokens.Add(entry);
            await _db.SaveChangesAsync();

            var jwt = await _jwtService.GenerateTokenAsync(user, TimeSpan.FromMinutes(15));
            return new TokenPair
            {
                AccessToken = jwt,
                RefreshToken = entry.RefreshValue
            };
        }

        public async Task<TokenPair> RefreshAsync(string deviceId, string refreshToken)
        {
            var existing = _repository.Get(x =>
            {
                x.DeviceIds = [deviceId];
                x.RefreshValues = [refreshToken];
            }).Data.FirstOrDefault();

            if (existing is null)
            {
                throw new UnauthorizedAccessException("Invalid refresh token.");
            }

            if (existing.User is null)
                throw new InvalidOperationException("Invalid UserId.");

            existing.RefreshValue = GenerateSecureToken();
            _db.RefreshTokens.Update(existing);
            await _db.SaveChangesAsync();

            var jwt = await _jwtService.GenerateTokenAsync(existing.User, TimeSpan.FromMinutes(15));
            return new TokenPair
            {
                AccessToken = jwt,
                RefreshToken = existing.RefreshValue
            };
        }

        public async Task RevokeAsync(Guid userId, string deviceId, string refreshToken)
        {
            var existing = _repository.Get(x =>
            {
                x.UserIds = [userId];
                x.DeviceIds = [deviceId];
                x.RefreshValues = [refreshToken];
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
                cursor is not null ? c => { c.Limit = cursor.Limit; c.After = cursor.After; }
            : null
            );
            return Task.FromResult(tokens);
        }
    }
}
