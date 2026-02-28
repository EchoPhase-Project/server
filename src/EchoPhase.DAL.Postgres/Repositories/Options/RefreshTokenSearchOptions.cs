namespace EchoPhase.DAL.Postgres.Repositories.Options
{
    public class RefreshTokenSearchOptions
    {
        public HashSet<Guid>? Ids
        {
            get; set;
        }
        public HashSet<Guid>? UserIds
        {
            get; set;
        }
        public HashSet<string>? DeviceIds
        {
            get; set;
        }
        public HashSet<string>? RefreshValues
        {
            get; set;
        }
    }
}
