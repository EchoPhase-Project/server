using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class Argon2Settings : IValidatable
    {
        public string Key { get; set; } = "argon2";

        public bool IsValid(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(Key))
            {
                errorMessage = "Key cannot be empty.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
