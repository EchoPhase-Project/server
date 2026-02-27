namespace EchoPhase.DAL.Postgres.Repositories.Options
{
    public class UserSearchOptions
    {
        public HashSet<Guid>? Ids { get; set; } = null;
        public HashSet<string>? Names { get; set; } = null;
        public HashSet<string>? UserNames { get; set; } = null;
        public HashSet<string>? Emails { get; set; } = null;
    }
}
