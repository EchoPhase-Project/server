namespace EchoPhase.Configuration.Database.Redis
{
    public interface IRedisSettings
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
