namespace EchoPhase.Repositories.Options
{
	public class UserOptions
	{
		public bool IncludeWebHooks { get; set; } = false;
		public bool IncludeJwtTokens { get; set; } = false;
		public bool IncludeDiscordTokens { get; set; } = false;
	}
}
