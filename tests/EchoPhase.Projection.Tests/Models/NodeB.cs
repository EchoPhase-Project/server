using EchoPhase.Projection.Attributes;

namespace EchoPhase.Projection.Tests.Models
{
    public class NodeB
    {
        [Expose] public string Label { get; set; } = "B";
        public NodeA? Parent { get; set; }
    }
}
