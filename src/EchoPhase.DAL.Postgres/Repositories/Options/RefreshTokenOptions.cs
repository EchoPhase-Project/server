namespace EchoPhase.DAL.Postgres.Repositories.Options
{
    public class RefreshTokenOptions
    {
        public bool IncludeUser { get; set; } = true;
        public bool IncludeClaims { get; set; } = true;
    }
}
