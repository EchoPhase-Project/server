using Microsoft.AspNetCore.Authorization;

using EchoPhase.Services;
using EchoPhase.Interfaces;
using EchoPhase.Requirements;

namespace EchoPhase.Handlers
{
	public class PermissionsAuthorizationHandler : AuthorizationHandler<PermissionsRequirement>
	{
		private readonly IPermissionsService _permissionsService;

		public PermissionsAuthorizationHandler(IPermissionsService permissionsService)
		{
			_permissionsService = permissionsService;
		}

		protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionsRequirement requirement)
		{
			var permissionsNames = requirement.Permissions;

			var userIntentClaim = context.User.FindFirst(PermissionsService.ClaimName);

			if (userIntentClaim != null && long.TryParse(userIntentClaim.Value, out long permissionValue))
			{
				if (_permissionsService.Has(permissionValue, permissionsNames))
				{
					context.Succeed(requirement);
				}
			}

			return Task.CompletedTask;
		}
	}
}
