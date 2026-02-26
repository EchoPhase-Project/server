namespace EchoPhase.Security.Authentication
{
    public class TokenPair
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
