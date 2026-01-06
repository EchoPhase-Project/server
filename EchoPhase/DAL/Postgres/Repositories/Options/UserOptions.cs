namespace EchoPhase.DAL.Postgres.Repositories.Options
{
    public class UserOptions
    {
        public bool IncludeWebHooks { get; set; } = false;
        public bool IncludeRefreshTokens { get; set; } = false;
        public bool IncludeDiscordTokens { get; set; } = false;
    }
}
