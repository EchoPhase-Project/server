namespace EchoPhase.DAL.Postgres.Repositories.Options
{
    public class DiscordTokenSearchOptions
    {
        public ISet<Guid>? Ids { get; set; } = null;
        public ISet<Guid>? UserIds { get; set; } = null;
        public ISet<string>? Names { get; set; } = null;
        public ISet<string>? Tokens { get; set; } = null;
    }
}
