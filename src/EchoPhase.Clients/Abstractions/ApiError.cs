namespace EchoPhase.Clients.Abstractions
{
    public sealed record ApiError(
        int Code,
        string? Message,
        IReadOnlyDictionary<string, string[]>? Details = null);
}
