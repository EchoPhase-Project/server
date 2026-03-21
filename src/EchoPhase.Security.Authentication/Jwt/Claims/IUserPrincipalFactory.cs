using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Security.Authentication.Jwt.Claims
{
    public interface IUserPrincipalFactory
    {
        Task<ClaimsPrincipal> CreateAsync(User user, ClaimsEnrichmentContext? context = null);
    }
}
