using EchoPhase.Interfaces;

namespace EchoPhase.Configurations.Models
{
	public class JwtSettings : ISettings
	{
		public string SecretKey { get; set; } = string.Empty;
		public string Issuer { get; set; } = string.Empty;
		public string Audience { get; set; } = string.Empty;
		public int ExpirationInMinutes { get; set; }

		public bool IsValid(out string errorMessage)
		{
			errorMessage = string.Empty;

			if (string.IsNullOrWhiteSpace(SecretKey) || SecretKey.Length < 16)
			{
				errorMessage = "SecretKey is either empty or too short. It must be at least 16 characters long.";
				return false;
			}

			if (string.IsNullOrWhiteSpace(Issuer))
			{
				errorMessage = "Issuer is required.";
				return false;
			}

			if (string.IsNullOrWhiteSpace(Audience))
			{
				errorMessage = "Audience is required.";
				return false;
			}

			if (ExpirationInMinutes <= 0)
			{
				errorMessage = "ExpirationInMinutes must be greater than zero.";
				return false;
			}

			return true;
		}
	}
}
