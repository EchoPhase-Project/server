using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class RoleSettings : IValidatable
    {
        public bool CheckRoles { get; set; } = true;

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;
            return true;
        }
    }
}
