namespace EchoPhase.Configuration.Database.Scylla
{
    public interface IScyllaSettings
    {
        string ContactPoint
        {
            get;
        }
        string Keyspace
        {
            get;
        }
        string Username
        {
            get;
        }
        string Password
        {
            get;
        }
    }
}
