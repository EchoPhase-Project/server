using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Interfaces
{
    public interface IJwtTokenService
    {
        Task<string> GenerateTokenAsync(User user, TimeSpan? lifetime = null);
        ClaimsPrincipal? ValidateToken(string token);
    }
}
