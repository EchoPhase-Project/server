namespace EchoPhase.Clients.Abstractions
{
    public interface IPageInfo
    {
        string? Cursor { get; }
        bool HasMore => Cursor is not null;
    }
}
