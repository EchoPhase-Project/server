using EchoPhase.Interfaces;

namespace EchoPhase.Configurations.Models
{
	public class AppSettings : ISettings
	{
		public bool IsValid(out string errorMessage)
		{
			errorMessage = string.Empty;
			return true;
		}
	}
}
