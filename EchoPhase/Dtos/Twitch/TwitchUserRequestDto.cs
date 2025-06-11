namespace EchoPhase.Dtos
{
    public class TwitchUserRequestDto
    {
        public List<string> Ids { get; set; } = new List<string>();
        public List<string> Logins { get; set; } = new List<string>();
    }
}
