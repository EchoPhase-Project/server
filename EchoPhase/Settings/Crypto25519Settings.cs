using EchoPhase.Enums;
using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class Crypto25519Settings : IValidatable
    {
        public AeadChoice aeadChoice = AeadChoice.ChaCha20Poly1305;

        public bool IsValid(out string errorMessage)
        {
            if (!Enum.IsDefined(typeof(AeadChoice), aeadChoice))
            {
                errorMessage = $"Invalid value {nameof(aeadChoice)}: {aeadChoice}";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
