using EchoPhase.Interfaces;
using EchoPhase.DAL.Redis.Interfaces;

namespace EchoPhase.Settings
{
    public class RedisSettings : IRedisSettings, IValidatable
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string InstanceName { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrEmpty(ConnectionString))
            {
                errorMessage = "Redis connection string is missing.";
                return false;
            }

            if (string.IsNullOrEmpty(InstanceName))
            {
                errorMessage = "Redis instance name is missing.";
                return false;
            }

            if (string.IsNullOrEmpty(TenantId))
            {
                errorMessage = "TenantId cannot be empty.";
                return false;
            }

            return true;
        }
    }
}
