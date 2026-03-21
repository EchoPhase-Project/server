namespace EchoPhase.Controllers.Api.v1.Dto.Auth
{
    public record ClaimsDto(
        IReadOnlyList<string> Scopes = default!,
        IReadOnlyList<string> Intents = default!,
        IReadOnlyDictionary<string, string[]> Permissions = default!
    );
}
