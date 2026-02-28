using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EchoPhase.Security.Antiforgery.Filters
{
    public class BearerOrValidateAntiForgeryTokenFilter : IAsyncActionFilter, IOrderedFilter
    {
        private readonly IAntiforgeryService _antiforgeryService;

        public int Order { get; set; } = int.MinValue;

        public BearerOrValidateAntiForgeryTokenFilter(IAntiforgeryService antiforgeryService)
        {
            _antiforgeryService = antiforgeryService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!IsBearerAuthenticated(context.HttpContext))
            {
                await _antiforgeryService.ValidateAsync();
            }

            await next();
        }

        private static readonly HashSet<string> _tokenSchemes =
        [
            JwtBearerDefaults.AuthenticationScheme
        ];

        private static bool IsBearerAuthenticated(HttpContext httpContext)
        {
            var authenticationType = httpContext.User.Identity?.AuthenticationType;
            Console.WriteLine(authenticationType);
            return authenticationType is not null && _tokenSchemes.Contains(authenticationType);
        }
    }
}
