// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;
using EchoPhase.Configuration.Authentication;
using EchoPhase.Configuration.Authentication.Refresh;
using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.Authentication.Jwt.Claims;
using EchoPhase.Security.Authentication.Secrets;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoPhase.Security.Authentication.Jwt.Providers
{
    public class SessionProvider : ISessionProvider
    {
        private const int TokenBytes = 32;

        private readonly SessionRepository _repository;
        private readonly PostgresContext _db;
        private readonly IJwtTokenProvider _jwtService;
        private readonly RefreshOptions _settings;
        private readonly UserRepository _userRepository;
        private readonly SessionTokenHasher _hasher;
        private readonly TimeProvider _time;

        public SessionProvider(
            SessionRepository repository,
            PostgresContext db,
            IJwtTokenProvider jwtService,
            IOptions<AuthenticationOptions> settings,
            UserRepository userRepository,
            SessionTokenHasher hasher,
            TimeProvider time)
        {
            _repository = repository;
            _db = db;
            _jwtService = jwtService;
            _settings = settings.Value.Refresh;
            _userRepository = userRepository;
            _hasher = hasher;
            _time = time;
        }

        private static string GenerateToken()
            => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

        public async Task<TokenInitial> CreateAsync(User user, string deviceId, CancellationToken ct = default)
        {
            var refreshToken = GenerateToken();

            var session = new Session
            {
                UserId = user.Id,
                DeviceId = deviceId,
                RefreshTokenHash = _hasher.Hash(refreshToken)
            };

            _db.Sessions.Add(session);
            await _db.SaveChangesAsync(ct);

            var jwt = await GenerateJwtAsync(user);
            return new TokenInitial
            {
                SessionId = session.Id,
                Tokens = new TokenPair
                {
                    AccessToken = jwt,
                    RefreshToken = refreshToken
                }
            };
        }

        public async Task<IServiceResult<TokenPair>> RefreshAsync(string refreshToken, CancellationToken ct = default)
        {
            var hash = _hasher.Hash(refreshToken);

            var session = await _repository.Query()
                .WithRefreshTokenHashes(hash)
                .FirstOrDefaultAsync(ct);

            if (session is not null)
            {
                try
                {
                    return ServiceResult<TokenPair>.Success(await RotateAsync(session, hash, ct));
                }
                catch (DbUpdateException)
                {
                    // A parallel request rotated the same token first (stale
                    // concurrency stamp or duplicate audit hash). Its audit row
                    // now exists, so fall through to the grace-period check.
                    _db.ChangeTracker.Clear();
                }
            }

            return await HandleRotatedTokenAsync(hash, ct);
        }

        private async Task<TokenPair> RotateAsync(Session session, string presentedHash, CancellationToken ct)
        {
            var user = await LoadUserAsync(session.UserId, ct);
            var next = GenerateToken();

            _db.SessionAudits.Add(new SessionAudit
            {
                SessionId = session.Id,
                TokenHash = presentedHash
            });

            session.RefreshTokenHash = _hasher.Hash(next);
            await _db.SaveChangesAsync(ct);

            return new TokenPair
            {
                AccessToken = await GenerateJwtAsync(user),
                RefreshToken = next
            };
        }

        private async Task<IServiceResult<TokenPair>> HandleRotatedTokenAsync(string hash, CancellationToken ct)
        {
            var audit = await _db.SessionAudits
                .AsNoTracking()
                .Where(a => a.TokenHash == hash)
                .Select(a => new { a.SessionId, a.CreatedAt })
                .FirstOrDefaultAsync(ct);
            if (audit is null)
                return InvalidToken();

            var latestRotation = await _db.SessionAudits
                .Where(a => a.SessionId == audit.SessionId)
                .MaxAsync(a => a.CreatedAt, ct);

            // The token that was replaced most recently, moments ago: a parallel
            // request won the race. Issue an access token without rotating; the
            // caller keeps the refresh token handed out by the winning request.
            if (audit.CreatedAt == latestRotation
                && audit.CreatedAt >= _time.GetUtcNow().UtcDateTime - _settings.ReuseGracePeriod)
            {
                var session = await _repository.Query()
                    .WithIds(audit.SessionId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ct);
                if (session is null)
                    return InvalidToken();

                return ServiceResult<TokenPair>.Success(new TokenPair
                {
                    AccessToken = await GenerateJwtAsync(await LoadUserAsync(session.UserId, ct)),
                    RefreshToken = null
                });
            }

            // An older token replayed: assume it was stolen and kill the session.
            var compromised = await _repository.Query()
                .WithIds(audit.SessionId)
                .FirstOrDefaultAsync(ct);
            if (compromised is null)
                return InvalidToken();

            var userId = compromised.UserId;
            _repository.Remove(compromised);
            await _repository.SaveAsync(ct);
            return ServiceResult<TokenPair>.Failure(new RefreshTokenReusedError(userId));
        }

        private static IServiceResult<TokenPair> InvalidToken()
            => ServiceResult<TokenPair>.Failure(e => e.Set(AuthErrors.InvalidRefreshToken, "Invalid refresh token."));

        public async Task RevokeAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
        {
            var existing = await _repository.Query()
                .WithIds(sessionId)
                .WithUserIds(userId)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                _repository.Remove(existing);
                await _repository.SaveAsync(ct);
            }
        }

        public async Task RevokeByTokenAsync(Guid userId, string refreshToken, CancellationToken ct = default)
        {
            var existing = await _repository.Query()
                .WithRefreshTokenHashes(_hasher.Hash(refreshToken))
                .WithUserIds(userId)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                _repository.Remove(existing);
                await _repository.SaveAsync(ct);
            }
        }

        public async Task RevokeAllAsync(Guid userId, CancellationToken ct = default)
        {
            var sessions = await _repository.Query()
                .WithUserIds(userId)
                .ToListAsync(ct);

            _db.Sessions.RemoveRange(sessions);
            await _db.SaveChangesAsync(ct);
        }

        public async Task RevokeOthersAsync(Guid userId, string? keepRefreshToken, CancellationToken ct = default)
        {
            Guid? keep = null;
            if (!string.IsNullOrEmpty(keepRefreshToken))
                keep = (await _repository.Query()
                    .WithRefreshTokenHashes(_hasher.Hash(keepRefreshToken))
                    .WithUserIds(userId)
                    .FirstOrDefaultAsync(ct))?.Id;

            var others = await _repository.Query()
                .WithUserIds(userId)
                .ToListAsync(ct);
            _db.Sessions.RemoveRange(others.Where(s => s.Id != keep));
            await _db.SaveChangesAsync(ct);
        }

        public Task<CursorPage<Session>> GetSessionsAsync(
            Guid userId, CursorOptions? cursor = null, CancellationToken ct = default)
        {
            var query = _repository.Query()
                .WithUserIds(userId);

            if (cursor is not null)
                query.WithCursor(cursor);

            return query.ToPageAsync(ct);
        }

        private async Task<User> LoadUserAsync(Guid userId, CancellationToken ct)
            => await _userRepository.Query()
                .WithIds(userId)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Invalid UserId.");

        // Interactive sessions get the full ceiling of the user's current roles.
        private Task<string> GenerateJwtAsync(User user)
            => _jwtService.GenerateTokenAsync(user, _settings.Lifetime, new ClaimsEnrichmentContext { User = user });
    }
}
