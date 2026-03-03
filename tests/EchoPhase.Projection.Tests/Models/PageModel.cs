using EchoPhase.Projection.Attributes;

namespace EchoPhase.Projection.Tests.Models
{
    public class PageModel<T>
    {
        [Expose] public IEnumerable<T> Data { get; set; } = Array.Empty<T>();
        [Expose] public string? NextCursor { get; set; }
        [Expose] public bool HasMore => NextCursor is not null;
    }
}

