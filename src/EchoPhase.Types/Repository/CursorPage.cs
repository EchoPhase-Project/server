using EchoPhase.Projection.Attributes;

namespace EchoPhase.Types.Repository
{
    public class CursorPage<T>
    {
        [Expose]
        public IEnumerable<T> Data { get; init; } = [];
        [Expose]
        public string? NextCursor
        {
            get; init;
        }
        [Expose]
        public bool HasMore => NextCursor is not null;
    }
}
