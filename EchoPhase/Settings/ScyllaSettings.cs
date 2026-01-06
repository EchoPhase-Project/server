using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class ScyllaSettings : IScyllaSettings, IValidatable
    {
        public string ContactPoint { get; set; } = string.Empty;
        public string Keyspace { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public bool IsValid(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(ContactPoint))
            {
                errorMessage = "ContactPoint cannot be empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Keyspace))
            {
                errorMessage = "Keyspace cannot be empty.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Username) && string.IsNullOrWhiteSpace(Password))
            {
                errorMessage = "Pssword required with username.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
