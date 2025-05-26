using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Requirements
{
	public class PermissionsRequirement : IAuthorizationRequirement
	{
		public ISet<string> Permissions { get; }

		public PermissionsRequirement(params IEnumerable<string> permissions)
		{
			Permissions = permissions.ToHashSet();
		}
	}
}
