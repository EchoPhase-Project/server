using EchoPhase.Security.Authentication.Jwt.Helpers;
using EchoPhase.Security.Authentication.Jwt.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Security.Authentication.Jwt.Claims;
using Microsoft.Extensions.Logging;
using EchoPhase.DAL.Postgres.Repositories;

namespace EchoPhase.Security.Authentication.Jwt
{
    public class RefreshSignInManager
    {
        private readonly UserManager<User>     _userManager;
        private readonly IRefreshTokenProvider _refresh;
        private readonly IHttpContextAccessor  _httpContext;
        private readonly IUserPrincipalFactory _principalFactory;
        private readonly ILogger<RefreshSignInManager> _logger;
        private readonly UserRepository _userRepository;

        public RefreshSignInManager(
            UserManager<User>               userManager,
            IRefreshTokenProvider           refresh,
            IHttpContextAccessor            httpContext,
            IUserPrincipalFactory           principalFactory,
            ILogger<RefreshSignInManager>   logger,
            UserRepository                  userRepository
        )
        {
            _userManager = userManager;
            _refresh     = refresh;
            _httpContext = httpContext;
            _logger      = logger;
            _principalFactory = principalFactory;
            _userRepository = userRepository;
        }

        public async Task LoginAsync(
            string username,
            string password,
            string deviceId)
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
            var user = _userRepository.Get(opts =>
            {
                opts.Ids = [userId];
            }).Data.FirstOrDefault() ??
                throw new InvalidOperationException("Missing user.");
            await _userManager.UpdateSecurityStampAsync(user);
            TokenCookieHelper.Clear(_httpContext.HttpContext!.Response);
        }

        public async Task LogoutAllAsync(Guid userId)
        {
            await _refresh.RevokeAllAsync(userId);
            var user = _userRepository.Get(opts =>
            {
                opts.Ids = [userId];
            }).Data.FirstOrDefault() ??
                throw new InvalidOperationException("Missing user.");
            await _userManager.UpdateSecurityStampAsync(user);
            TokenCookieHelper.Clear(_httpContext.HttpContext!.Response);
        }
    }
}
