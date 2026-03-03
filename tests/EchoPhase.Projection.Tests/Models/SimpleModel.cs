using EchoPhase.Projection.Attributes;

namespace EchoPhase.Projection.Tests.Models
{
    public class SimpleModel
    {
        [Expose] public Guid Id { get; set; } = Guid.NewGuid();
        [Expose] public string Name { get; set; } = "Test";
        public string Secret { get; set; } = "hidden";
        public int InternalCode { get; set; } = 42;
    }
}
