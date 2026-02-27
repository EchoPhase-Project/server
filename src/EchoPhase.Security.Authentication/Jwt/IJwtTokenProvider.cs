using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Security.Authentication.Jwt
{
    public interface IJwtTokenProvider
    {
        Task<string> GenerateTokenAsync(User user, TimeSpan? lifetime = null);
        ClaimsPrincipal? ValidateToken(string token);
    }
}
