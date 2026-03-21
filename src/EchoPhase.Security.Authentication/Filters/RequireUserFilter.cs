using EchoPhase.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EchoPhase.Security.Authentication.Filters
{
    public class RequireUserFilter : IAsyncActionFilter
    {
        private readonly IUserService _userService;

        public RequireUserFilter(IUserService userService)
        {
            _userService = userService;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var user = await _userService.GetAsync(context.HttpContext.User);

            if (user is null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            context.HttpContext.Items["User"] = user;

            await next();
        }
    }
}
