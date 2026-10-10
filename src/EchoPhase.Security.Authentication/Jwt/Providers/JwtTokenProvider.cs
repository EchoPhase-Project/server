// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EchoPhase.Configuration.Authentication;
using EchoPhase.Configuration.Authentication.Bearer;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.Authentication.Jwt.Claims;
using EchoPhase.Security.Authentication.Jwt.Verdicts;
using EchoPhase.Security.BitMasks;
using EchoPhase.Security.Cryptography.Vaults;
using EchoPhase.Types.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EchoPhase.Security.Authentication.Jwt.Providers
{
    public class JwtTokenProvider : IJwtTokenProvider
    {
        private readonly ILogger<JwtTokenProvider> _logger;
        private readonly BearerOptions _settings;
        private readonly UserRepository _userRepository;
        private readonly ApiClientRepository _apiClients;
        private readonly ImpersonationRepository _impersonations;
        private readonly IUserPrincipalFactory _principalFactory;
        private readonly IPrincipalVerdictCache _verdicts;
        private readonly ResourcePermissionsBitMask _permissions;
        private readonly KeyRing _keys;
        private readonly TimeProvider _time;

        public JwtTokenProvider(
            ILogger<JwtTokenProvider> logger,
            IOptions<AuthenticationOptions> settings,
            KeyRing keys,
            UserRepository userRepository,
            ApiClientRepository apiClients,
            ImpersonationRepository impersonations,
            IUserPrincipalFactory principalFactory,
            IPrincipalVerdictCache verdicts,
            ResourcePermissionsBitMask permissions,
            TimeProvider time
        )
        {
            _logger = logger;
            _settings = settings.Value.Bearer;
            _userRepository = userRepository;
            _apiClients = apiClients;
            _impersonations = impersonations;
            _principalFactory = principalFactory;
            _verdicts = verdicts;
            _permissions = permissions;
            // Scoped: the signing key is read from the ring when used, so building
            // a provider per request costs no Redis round trip.
            _keys = keys;
            _time = time;
        }

        private byte[] SigningKey => _keys.Get(_settings.Key);

        public virtual async Task<string> GenerateTokenAsync(
            User user,
            TimeSpan? lifetime = null,
            ClaimsEnrichmentContext? context = null)
        {
            if (user is null) throw new ArgumentNullException(nameof(user));

            var principal = await _principalFactory.CreateAsync(user, context);
            var identity = (ClaimsIdentity)principal.Identity!;

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = identity,
                Expires = _time.GetUtcNow().UtcDateTime + (lifetime ?? _settings.Lifetime),
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(SigningKey),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        }

        public virtual async Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken ct = default)
        {
            if (token is null)
                throw new ArgumentNullException(nameof(token));

            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.InboundClaimTypeMap.Clear();
            var tokenValidationParameters = new TokenValidationParameters
            {
                AuthenticationType = JwtBearerDefaults.AuthenticationScheme,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(SigningKey),
                ValidateIssuer = true,
                ValidIssuer = _settings.ValidIssuer,
                ValidateAudience = true,
                ValidAudiences = _settings.ValidAudiences,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            ClaimsPrincipal claims;
            try
            {
                claims = tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("JWT signature/lifetime validation failed: {Message}", ex.Message);
                return null;
            }

            return await CheckPrincipalAsync(claims, ct) switch
            {
                PrincipalState.Valid => claims,
                // Lets the handler fall back to a silent refresh.
                PrincipalState.StampMismatch => throw new SecurityTokenExpiredException("Security stamp mismatch."),
                _ => null
            };
        }

        public async Task<bool> IsPrincipalValidAsync(ClaimsPrincipal principal, CancellationToken ct = default)
        {
            var exp = principal.FindFirstValue(JwtRegisteredClaimNames.Exp);
            if (exp is null
                || !long.TryParse(exp, out var seconds)
                || DateTimeOffset.FromUnixTimeSeconds(seconds) <= _time.GetUtcNow())
                return false;

            return await CheckPrincipalAsync(principal, ct) == PrincipalState.Valid;
        }

        private enum PrincipalState
        {
            Valid,
            Invalid,
            StampMismatch
        }

        /// <summary>
        /// Checks that still matter after the signature: the user, their security
        /// stamp, and the API client or impersonation the token belongs to.
        /// A cached positive verdict skips the database.
        /// </summary>
        private async Task<PrincipalState> CheckPrincipalAsync(ClaimsPrincipal claims, CancellationToken ct)
        {
            // Permissions are encoded against the current set of resources and
            // actions; a token from before that set changed cannot be read.
            // Treat it like a stamp change so sessions refresh silently.
            if (claims.FindFirstValue(_permissions.ClaimName) is { } permissions
                && !_permissions.Deserialize(permissions).Successful)
            {
                _logger.LogInformation("JWT permissions use an outdated encoding; token must be reissued");
                return PrincipalState.StampMismatch;
            }

            if (await _verdicts.IsKnownValidAsync(claims))
                return PrincipalState.Valid;

            var state = await CheckPrincipalInDatabaseAsync(claims, ct);
            if (state == PrincipalState.Valid)
                await _verdicts.RememberValidAsync(claims);

            return state;
        }

        private async Task<PrincipalState> CheckPrincipalInDatabaseAsync(ClaimsPrincipal claims, CancellationToken ct)
        {
            var sub = claims.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (sub is null || !Guid.TryParse(sub, out var userId))
            {
                _logger.LogWarning("JWT missing or invalid 'sub' claim");
                return PrincipalState.Invalid;
            }

            var user = await _userRepository.Query()
                .WithIds(userId)
                .FirstOrDefaultAsync(ct);

            if (user is null)
            {
                _logger.LogWarning("User {UserId} not found in DB", userId);
                return PrincipalState.Invalid;
            }

            var stamp = claims.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            if (stamp != user.SecurityStamp)
            {
                _logger.LogWarning("SecurityStamp mismatch for user {UserId}", userId);
                return PrincipalState.StampMismatch;
            }

            // Tokens issued to an API client die with the client, not just at expiry.
            var clientIdRaw = claims.FindFirstValue(EchoPhaseClaimTypes.ClientId);
            if (clientIdRaw is not null)
            {
                var now = _time.GetUtcNow().UtcDateTime;
                if (!Guid.TryParse(clientIdRaw, out var clientId)
                    || !await _apiClients.Query()
                        .WithIds(clientId)
                        .WithUserIds(userId)
                        .AnyAsync(c => c.ExpiresAt == null || c.ExpiresAt > now, ct))
                {
                    _logger.LogWarning("API client {ClientId} of user {UserId} is revoked or expired", clientIdRaw, userId);
                    return PrincipalState.Invalid;
                }
            }

            if (!await IsImpersonationValidAsync(claims, userId, ct))
                return PrincipalState.Invalid;

            return PrincipalState.Valid;
        }

        /// <summary>
        /// An <c>act</c> token is only good while its record is open and the
        /// actor still holds the stamp they started with.
        /// </summary>
        private async Task<bool> IsImpersonationValidAsync(ClaimsPrincipal claims, Guid targetId, CancellationToken ct)
        {
            var hasAct = claims.HasClaim(c => c.Type == EchoPhaseClaimTypes.Act);
            var rawId = claims.FindFirstValue(EchoPhaseClaimTypes.ImpersonationId);

            if (!hasAct && rawId is null)
                return true;

            if (!hasAct || !Guid.TryParse(rawId, out var impersonationId))
            {
                _logger.LogWarning("Malformed impersonation token for user {UserId}", targetId);
                return false;
            }

            var impersonation = await _impersonations.Query()
                .WithIds(impersonationId)
                .WithTargetIds(targetId)
                .Active(_time.GetUtcNow().UtcDateTime)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            var actor = impersonation is null
                ? null
                : await _userRepository.Query().WithIds(impersonation.ActorId).FirstOrDefaultAsync(ct);

            if (impersonation is null || actor is null || actor.SecurityStamp != impersonation.ActorSecurityStamp)
            {
                _logger.LogWarning("Impersonation {ImpersonationId} of user {UserId} is no longer active", impersonationId, targetId);
                return false;
            }

            return true;
        }
    }
}
