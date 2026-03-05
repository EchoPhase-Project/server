namespace EchoPhase.Security.Authentication.Jwt.Providers
{
    public class TokenInitial
    {
        public Guid Id { get; set; } = Guid.Empty;
        public TokenPair Tokens { get; set; } = new();
    }
}
