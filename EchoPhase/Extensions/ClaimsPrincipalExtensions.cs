using System.Security.Claims;

namespace EchoPhase.Extensions
{
	public static class ClaimsPrincipalExtensions
	{
		public static Guid? GetUserId(this ClaimsPrincipal user)
		{
			var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			return Guid.TryParse(id, out var guid) ? guid : null;
		}
	}
}
