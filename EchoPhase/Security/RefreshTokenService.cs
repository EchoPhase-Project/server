using System.Security.Cryptography;
using EchoPhase.DAL.Postgres;
using EchoPhase.Interfaces;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Security.Models;

namespace EchoPhase.Security
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly PostgresContext _db;
        private readonly IJwtTokenService _jwtService;

        public RefreshTokenService(
            PostgresContext db,
            IJwtTokenService jwtService
        )
        {
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
            var existing = _db.RefreshTokens
                .FirstOrDefault(r =>
                    r.DeviceId == deviceId &&
                    r.RefreshValue == refreshToken);

            if (existing is null)
                throw new NullReferenceException("Invalid refresh token.");

            if (existing.User is null)
                throw new NullReferenceException("Invalid UserId.");

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

        public async Task RevokeAsync(string deviceId, string refreshToken)
        {
            var existing = _db.RefreshTokens
                .FirstOrDefault(r =>
                    r.DeviceId == deviceId &&
                    r.RefreshValue == refreshToken);

            if (existing is not null)
            {
                _db.RefreshTokens.Remove(existing);
                await _db.SaveChangesAsync();
            }
        }

        public async Task RevokeAllAsync(Guid userId)
        {
            var tokens = _db.RefreshTokens.Where(r => r.UserId == userId);
            _db.RefreshTokens.RemoveRange(tokens);
            await _db.SaveChangesAsync();
        }
    }
}
