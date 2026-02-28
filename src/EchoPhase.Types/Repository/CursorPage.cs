namespace EchoPhase.Types.Repository
{
    public class CursorPage<T>
    {
        public IEnumerable<T> Data { get; init; } = [];
        public string? NextCursor
        {
            get; init;
        }
        public bool HasMore => NextCursor is not null;
    }
}
