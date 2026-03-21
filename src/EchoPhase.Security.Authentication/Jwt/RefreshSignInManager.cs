// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.Authentication.Jwt.Claims;
using EchoPhase.Security.Authentication.Jwt.Exceptions;
using EchoPhase.Security.Authentication.Jwt.Helpers;
using EchoPhase.Security.Authentication.Jwt.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace EchoPhase.Security.Authentication.Jwt
{
    public class RefreshSignInManager
    {
        private readonly UserManager<User> _userManager;
        private readonly IRefreshTokenProvider _refresh;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IUserPrincipalFactory _principalFactory;
        private readonly ILogger<RefreshSignInManager> _logger;
        private readonly UserRepository _userRepository;

        public RefreshSignInManager(
            UserManager<User> userManager,
            IRefreshTokenProvider refresh,
            IHttpContextAccessor httpContext,
            IUserPrincipalFactory principalFactory,
            ILogger<RefreshSignInManager> logger,
            UserRepository userRepository)
        {
            _userManager = userManager;
            _refresh = refresh;
            _httpContext = httpContext;
            _principalFactory = principalFactory;
            _logger = logger;
            _userRepository = userRepository;
        }

        public async Task LoginAsync(string username, string password, string deviceId)
        {
            var user = await _userManager.FindByNameAsync(username)
                       ?? throw new UnauthorizedAccessException("Invalid credentials.");

            if (await _userManager.IsLockedOutAsync(user))
                throw new UnauthorizedAccessException("Account locked out.");

            var valid = await _userManager.CheckPasswordAsync(user, password);
            if (!valid)
            {
                await _userManager.AccessFailedAsync(user);
                throw new UnauthorizedAccessException("Invalid credentials.");
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            var context = _httpContext.HttpContext
                          ?? throw new InvalidOperationException("HttpContext is not available.");

            var initial = await _refresh.CreateAsync(user, deviceId);
            context.User = await _principalFactory.CreateAsync(user);

            TokenCookieHelper.WriteInitial(context.Response, context.Request, initial);
        }

        public async Task LogoutAsync(Guid userId, Guid tokenId)
        {
            await _refresh.RevokeAsync(userId, tokenId);
            await InvalidateUserAsync(userId);
            TokenCookieHelper.Clear(_httpContext.HttpContext!.Response);
        }

        public async Task LogoutAllAsync(Guid userId)
        {
            await _refresh.RevokeAllAsync(userId);
            await InvalidateUserAsync(userId);
            TokenCookieHelper.Clear(_httpContext.HttpContext!.Response);
        }

        public async Task<TokenPair> RefreshAsync(Guid refreshId, string refreshToken)
        {
            try
            {
                return await _refresh.RefreshAsync(refreshId, refreshToken);
            }
            catch (RefreshTokenReusedException ex)
            {
                await InvalidateUserAsync(ex.UserId);
                throw;
            }
        }

        public async Task RevokeSessionAsync(Guid userId, Guid tokenId)
        {
            await _refresh.RevokeAsync(userId, tokenId);
            await InvalidateUserAsync(userId);
        }

        private async Task InvalidateUserAsync(Guid userId)
        {
            var user = _userRepository.Query()
                .WithIds(userId)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("Missing user.");

            await _userManager.UpdateSecurityStampAsync(user);
        }
    }
}
