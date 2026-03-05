using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace EchoPhase.Security.Authentication.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal principal) =>
            Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? throw new InvalidOperationException("UserId claim missing."));

        public static string GetUserName(this ClaimsPrincipal principal) =>
            principal.FindFirstValue(JwtRegisteredClaimNames.Name)
                ?? throw new InvalidOperationException("UserName claim missing.");
    }
}
