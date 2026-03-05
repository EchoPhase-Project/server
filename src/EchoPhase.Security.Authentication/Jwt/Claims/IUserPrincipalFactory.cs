using EchoPhase.DAL.Postgres.Models;
using System.Security.Claims;
using EchoPhase.Security.Authentication.Jwt.Claims;

namespace EchoPhase.Security.Authentication.Jwt.Claims
{
    public interface IUserPrincipalFactory
    {
        Task<ClaimsPrincipal> CreateAsync(User user, ClaimsEnrichmentContext? context = null);
    }
}
