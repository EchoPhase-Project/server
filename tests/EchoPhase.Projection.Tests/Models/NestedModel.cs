using EchoPhase.Projection.Attributes;

namespace EchoPhase.Projection.Tests.Models
{
    public class NestedModel
    {
        [Expose] public Guid Id { get; set; } = Guid.NewGuid();
        [Expose] public AddressModel Address { get; set; } = new();
        public string Hidden { get; set; } = "hidden";
    }
}
