using EchoPhase.Interfaces;
using System.Text.RegularExpressions;

namespace EchoPhase.Configurations.Models
{
	public class TwitchSettings : ISettings
	{
		public string ClientId { get; set; } = string.Empty;
		public string AccessToken { get; set; } = string.Empty;

		private static readonly Regex ClientIdRegex = new Regex(@"^[a-zA-Z0-9]{24}$", RegexOptions.Compiled);
		private static readonly Regex AccessTokenRegex = new Regex(@"^oauth:[a-zA-Z0-9]{30,40}$", RegexOptions.Compiled);

		public bool IsValid(out string errorMesage)
		{
			errorMesage = string.Empty;

			if (string.IsNullOrEmpty(ClientId))
			{
				errorMesage = "ClientId cannot be null or empty.";
				return false;
			}

			if (!ClientIdRegex.IsMatch(ClientId))
			{
				errorMesage = "ClientId format is invalid.";
				return false;
			}

			if (string.IsNullOrEmpty(AccessToken))
			{
				errorMesage = "AccessToken cannot be null or empty.";
				return false;
			}

			if (!AccessTokenRegex.IsMatch(AccessToken))
			{
				errorMesage = "AccessToken format is invalid.";
				return false;
			}

			return true;
		}
	}
}
