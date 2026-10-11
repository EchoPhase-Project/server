// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Antiforgery;
using EchoPhase.Security.Authentication.Jwt;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.Http;

namespace EchoPhase.Security.Authentication
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly RefreshSignInManager _signInManager;
        private readonly IAntiforgeryService _antiforgery;
        private readonly IHttpContextAccessor _httpContext;

        public AuthenticationService(
            RefreshSignInManager signInManager,
            IAntiforgeryService antiforgery,
            IHttpContextAccessor httpContext)
        {
            _signInManager = signInManager;
            _antiforgery = antiforgery;
            _httpContext = httpContext;
        }

        public async Task<IServiceResult> LoginAsync(string username, string password, CancellationToken ct = default)
        {
            var result = await _signInManager.LoginAsync(username, password, ResolveDeviceId(), ct);
            if (result.Successful)
                _antiforgery.Set();
            return result;
        }

        public async Task<IServiceResult> LogoutAsync(Guid userId, CancellationToken ct = default)
        {
            var context = _httpContext.HttpContext!;

            if (context.Request.Cookies.TryGetValue(CookieNames.RefreshToken, out var refreshToken)
                && !string.IsNullOrEmpty(refreshToken))
            {
                await _signInManager.LogoutAsync(userId, refreshToken, ct);
            }

            _antiforgery.Remove();
            return ServiceResult.Success();
        }

        public async Task<IServiceResult> LogoutAllAsync(Guid userId, CancellationToken ct = default)
        {
            await _signInManager.LogoutAllAsync(userId, ct);
            _antiforgery.Remove();
            return ServiceResult.Success();
        }

        public async Task<IServiceResult> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
        {
            await _signInManager.RevokeSessionAsync(userId, sessionId, ct);
            return ServiceResult.Success();
        }

        private string ResolveDeviceId()
            => _httpContext.HttpContext!.Request.Headers.UserAgent.ToString();
    }
}
