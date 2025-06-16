using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class AesSettings : IValidatable
    {
        public int TagSize { get; set; } = 16;
        public int NonceSize { get; set; } = 12;
        public string Key { get; set; } = "aes";

        public bool IsValid(out string errorMessage)
        {
            if (TagSize <= 0)
            {
                errorMessage = "TagSize must be positive.";
                return false;
            }

            if (NonceSize <= 0)
            {
                errorMessage = "NonceSize must be positive.";
                return false;
            }

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
