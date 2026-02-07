namespace EchoPhase.Configuration.Settings
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
