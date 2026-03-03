using EchoPhase.Projection.Attributes;

namespace EchoPhase.Projection.Tests.Models
{
    public class UserModel
    {
        [Expose] public Guid Id { get; set; } = Guid.NewGuid();
        [Expose] public string Name { get; set; } = "User";
        public string PasswordHash { get; set; } = "hash";
        public ICollection<TokenModel> Tokens { get; set; } = new List<TokenModel>();
        public ICollection<SimpleModel> Items { get; set; } = new List<SimpleModel>();
    }
}
