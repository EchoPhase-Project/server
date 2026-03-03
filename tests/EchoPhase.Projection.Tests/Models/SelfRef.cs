using EchoPhase.Projection.Attributes;

namespace EchoPhase.Projection.Tests.Models
{
    public class SelfRef
    {
        [Expose] public string Name { get; set; } = "me";
        public SelfRef? Self { get; set; }
    }
}
