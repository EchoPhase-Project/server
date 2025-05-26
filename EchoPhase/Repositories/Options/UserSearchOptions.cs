namespace EchoPhase.Repositories.Options
{
	public class UserSearchOptions
	{
		public ISet<Guid>? Ids { get; set; } = null;
		public ISet<string>? Names { get; set; } = null;
		public ISet<string>? UserNames { get; set; } = null;
		public ISet<string>? Emails { get; set; } = null;
	}
}
