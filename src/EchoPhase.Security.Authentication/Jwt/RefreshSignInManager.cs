// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.Authentication.Jwt.Claims;
using EchoPhase.Security.Authentication.Jwt.Helpers;
using EchoPhase.Security.Authentication.Jwt.Providers;
using EchoPhase.Security.Hashers;
using EchoPhase.Types.Result;
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

        /// <summary>
        /// Checks the password and starts a session (token cookies). A wrong user name
        /// or password is a failure (<see cref="AuthErrors.InvalidCredentials"/>,
        /// <see cref="AuthErrors.LockedOut"/>), not an exception.
        /// </summary>
        public async Task<IServiceResult> LoginAsync(string username, string password, string deviceId, CancellationToken ct = default)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user is null)
                return InvalidCredentials();

            if (await _userManager.IsLockedOutAsync(user))
                return ServiceResult.Failure(e => e.Set(AuthErrors.LockedOut, "Account locked out."));

            var valid = await _hashing.RunAsync(() => _userManager.CheckPasswordAsync(user, password), ct);
            if (!valid)
            {
                await _userManager.AccessFailedAsync(user);
                return InvalidCredentials();
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            var context = _httpContext.HttpContext
                          ?? throw new InvalidOperationException("HttpContext is not available.");

            var initial = await _refresh.CreateAsync(user, deviceId, ct);
            context.User = await _principalFactory.CreateAsync(user);

            TokenCookieHelper.WriteInitial(context.Response, context.Request, initial);
            return ServiceResult.Success();
        }

        /// <summary>
        /// Changes the user's password when the current one is right, then
        /// signs out their other devices (this one keeps its session: the
        /// changed security stamp makes it refresh its access token). A wrong
        /// current password counts as a failed sign-in, towards the lockout.
        /// </summary>
        public async Task<IdentityResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword,
            string? keepRefreshToken, CancellationToken ct = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new InvalidOperationException("Missing user.");
            if (await _userManager.IsLockedOutAsync(user))
                return IdentityResult.Failed(new IdentityError { Code = AuthErrors.LockedOut, Description = "Account locked out." });

            var result = await _hashing.RunAsync(
                () => _userManager.ChangePasswordAsync(user, currentPassword, newPassword), ct);
            if (!result.Succeeded)
            {
                if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
                    await _userManager.AccessFailedAsync(user);
                return result;
            }

            await _userManager.ResetAccessFailedCountAsync(user);
            await _refresh.RevokeOthersAsync(userId, keepRefreshToken, ct);
            return result;
        }

        private static IServiceResult InvalidCredentials()
            => ServiceResult.Failure(e => e.Set(AuthErrors.InvalidCredentials, "Invalid credentials."));

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

        /// <summary>
        /// Rotates the session's tokens (see <see cref="ISessionProvider.RefreshAsync"/>).
        /// On a replayed token the user's other tokens are invalidated as well.
        /// </summary>
        public async Task<IServiceResult<TokenPair>> RefreshAsync(string refreshToken, CancellationToken ct = default)
        {
            var result = await _refresh.RefreshAsync(refreshToken, ct);
            if (result.Error is RefreshTokenReusedError reused)
                await InvalidateUserAsync(reused.UserId, ct);
            return result;
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
