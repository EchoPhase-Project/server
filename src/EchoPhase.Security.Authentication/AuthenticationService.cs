using EchoPhase.Types.Result;
using Microsoft.AspNetCore.Http;
using EchoPhase.Security.Authentication.Jwt;
using EchoPhase.Security.Antiforgery;
using EchoPhase.Security.Authentication;
using Microsoft.Extensions.Logging;

public class AuthenticationService : IAuthenticationService
{
    private readonly RefreshSignInManager _signInManager;
    private readonly IAntiforgeryService  _antiforgery;
    private readonly IHttpContextAccessor _httpContext;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        RefreshSignInManager signInManager,
        IAntiforgeryService  antiforgery,
        IHttpContextAccessor httpContext,
        ILogger<AuthenticationService> logger
    )
    {
        _signInManager = signInManager;
        _antiforgery   = antiforgery;
        _httpContext   = httpContext;
        _logger = logger;
    }

    public async Task<IServiceResult> LoginAsync(string username, string password)
    {
        try
        {
            await _signInManager.LoginAsync(username, password, ResolveDeviceId());
            var ctx = _httpContext.HttpContext;
            _antiforgery.Set();
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task<IServiceResult> LogoutAsync(Guid userId)
    {
        try
        {
            var context = _httpContext.HttpContext!;

            if (context.Request.Cookies.TryGetValue(CookieNames.RefreshId, out var raw)
                && Guid.TryParse(raw, out var tokenId))
            {
                await _signInManager.LogoutAsync(userId, tokenId);
            }

            _antiforgery.Remove();
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task<IServiceResult> LogoutAllAsync(Guid userId)
    {
        try
        {
            await _signInManager.LogoutAllAsync(userId);
            _antiforgery.Remove();
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private string ResolveDeviceId()
        => _httpContext.HttpContext!.Request.Headers.UserAgent.ToString();

    private static IServiceResult Fail(Exception ex)
        => ServiceResult.Failure(e => e.Set(
            ex.GetType().Name.Replace("Exception", string.Empty),
            ex.Message));
}
