using EchoPhase.Interfaces;

namespace EchoPhase.Configurations.Models
{
	public class RoleSettings : ISettings
	{
		public bool CheckRoles { get; set; } = true;

		public bool IsValid(out string errorMessage)
		{
			errorMessage = string.Empty;

			return true;
		}
	}
}
