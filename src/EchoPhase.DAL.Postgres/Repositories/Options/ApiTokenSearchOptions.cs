namespace EchoPhase.DAL.Postgres.Repositories.Options
{
    public class DiscordTokenSearchOptions
    {
        public HashSet<Guid>? Ids { get; set; } = null;
        public HashSet<Guid>? UserIds { get; set; } = null;
        public HashSet<string>? Names { get; set; } = null;
        public HashSet<string>? Tokens { get; set; } = null;
    }
}
