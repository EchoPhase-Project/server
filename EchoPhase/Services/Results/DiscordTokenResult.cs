using EchoPhase.Models;
using EchoPhase.Interfaces;

namespace EchoPhase.Services.Results
{
	public class DiscordTokenResult : IDiscordTokenResult
	{
		public ISet<DiscordToken> Affected { get; set; } = new HashSet<DiscordToken>();
		public ISet<string> Errors { get; set; } = new HashSet<string>();
		public bool IsSucceeded { get; set; } = true;
	}
}
