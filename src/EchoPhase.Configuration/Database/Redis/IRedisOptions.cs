namespace EchoPhase.Configuration.Database.Redis
{
    public interface IRedisOptions
    {
        string ConnectionString
        {
            get;
        }
        string InstanceName
        {
            get;
        }
        Guid TenantId
        {
            get;
        }
    }
}
