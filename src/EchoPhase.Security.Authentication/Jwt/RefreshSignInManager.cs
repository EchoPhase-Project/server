// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.Authentication.Jwt.Claims;
using EchoPhase.Security.Authentication.Jwt.Exceptions;
using EchoPhase.Security.Authentication.Jwt.Helpers;
using EchoPhase.Security.Authentication.Jwt.Providers;
using EchoPhase.Security.Hashers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace EchoPhase.Security.Authentication.Jwt
{
    public class RefreshSignInManager
    {
        private readonly UserManager<User> _userManager;
        private readonly ISessionProvider _refresh;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IUserPrincipalFactory _principalFactory;
        private readonly ILogger<RefreshSignInManager> _logger;
        private readonly UserRepository _userRepository;
        private readonly PasswordHashingGate _hashing;

        public RefreshSignInManager(
            UserManager<User> userManager,
            PasswordHashingGate hashing,
            ISessionProvider refresh,
            IHttpContextAccessor httpContext,
            IUserPrincipalFactory principalFactory,
            ILogger<RefreshSignInManager> logger,
            UserRepository userRepository)
        {
            _userManager = userManager;
            _hashing = hashing;
            _refresh = refresh;
            _httpContext = httpContext;
            _principalFactory = principalFactory;
            _logger = logger;
            _userRepository = userRepository;
        }

        public async Task LoginAsync(string username, string password, string deviceId, CancellationToken ct = default)
        {
            var user = await _userManager.FindByNameAsync(username)
                       ?? throw new UnauthorizedAccessException("Invalid credentials.");

            if (await _userManager.IsLockedOutAsync(user))
                throw new UnauthorizedAccessException("Account locked out.");

            var valid = await _hashing.RunAsync(() => _userManager.CheckPasswordAsync(user, password), ct);
            if (!valid)
            {
                await _userManager.AccessFailedAsync(user);
                throw new UnauthorizedAccessException("Invalid credentials.");
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            var context = _httpContext.HttpContext
                          ?? throw new InvalidOperationException("HttpContext is not available.");

            var initial = await _refresh.CreateAsync(user, deviceId, ct);
            context.User = await _principalFactory.CreateAsync(user);

            TokenCookieHelper.WriteInitial(context.Response, context.Request, initial);
        }

        public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct = default)
        {
            await _refresh.RevokeByTokenAsync(userId, refreshToken, ct);
            await InvalidateUserAsync(userId, ct);
            var context = _httpContext.HttpContext
                          ?? throw new InvalidOperationException("HttpContext is not available.");
            TokenCookieHelper.Clear(context.Response);
        }

        public async Task LogoutAllAsync(Guid userId, CancellationToken ct = default)
        {
            await _refresh.RevokeAllAsync(userId, ct);
            await InvalidateUserAsync(userId, ct);
            var context = _httpContext.HttpContext
                          ?? throw new InvalidOperationException("HttpContext is not available.");
            TokenCookieHelper.Clear(context.Response);
        }

        public async Task<TokenPair> RefreshAsync(string refreshToken, CancellationToken ct = default)
        {
            try
            {
                return await _refresh.RefreshAsync(refreshToken, ct);
            }
            catch (RefreshTokenReusedException ex)
            {
                await InvalidateUserAsync(ex.UserId, ct);
                throw;
            }
        }

        public async Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
        {
            await _refresh.RevokeAsync(userId, sessionId, ct);
            await InvalidateUserAsync(userId, ct);
        }

        private async Task InvalidateUserAsync(Guid userId, CancellationToken ct)
        {
            var user = await _userRepository.Query()
                .WithIds(userId)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Missing user.");

            await _userManager.UpdateSecurityStampAsync(user);
        }
    }
}
