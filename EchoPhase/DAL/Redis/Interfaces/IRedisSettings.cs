namespace EchoPhase.DAL.Redis.Interfaces
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
        string TenantId
        {
            get;
        }
    }
}
