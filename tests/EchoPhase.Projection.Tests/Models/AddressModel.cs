using EchoPhase.Projection.Attributes;

namespace EchoPhase.Projection.Tests.Models
{
    public class AddressModel
    {
        [Expose] public string City { get; set; } = "Moscow";
        [Expose] public string Country { get; set; } = "RU";
        public string InternalCode { get; set; } = "secret";
    }
}
