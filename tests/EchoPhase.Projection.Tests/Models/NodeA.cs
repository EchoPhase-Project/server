using EchoPhase.Projection.Attributes;

namespace EchoPhase.Projection.Tests.Models
{
    public class NodeA
    {
        [Expose] public string Label { get; set; } = "A";
        [Expose] public NodeB? Child { get; set; }
    }
}
