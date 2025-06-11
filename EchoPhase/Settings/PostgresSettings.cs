using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class PostgresSettings : IValidatable
    {
        public string ConnectionString { get; set; } = string.Empty;

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrEmpty(ConnectionString))
            {
                errorMessage = "Postgres connection string is missing.";
                return false;
            }

            return true;
        }
    }
}
