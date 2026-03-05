using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Security.Authentication.Jwt.Claims
{
    public class ClaimsEnrichmentContext
    {
        public required User User { get; init; }

        public IReadOnlyList<string> RequestedScopes { get; init; } = [];
        public IReadOnlyList<string> RequestedIntents { get; init; } = [];

        public IReadOnlyDictionary<string, string[]> RequestedPermissions { get; init; }  // string[] → string
            = new Dictionary<string, string[]>();
    }
}
