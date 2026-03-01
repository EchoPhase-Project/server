namespace EchoPhase.Configuration.Database.Scylla
{
    public interface IScyllaOptions
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
