namespace EchoPhase.Clients.Abstractions
{
    public sealed record PageInfo(string? Cursor) : IPageInfo;
}
