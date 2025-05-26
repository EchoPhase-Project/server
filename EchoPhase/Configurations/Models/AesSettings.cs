using EchoPhase.Interfaces;

namespace EchoPhase.Configurations.Models
{
	public class AesSettings : ISettings
	{
		public string SecretKey { get; set; } = string.Empty;

		public bool IsValid(out string errorMessage)
		{
			errorMessage = string.Empty;

			if (string.IsNullOrWhiteSpace(SecretKey))
			{
				errorMessage = "Key cannot be null or empty.";
				return false;
			}

			return true;
		}
	}
}
