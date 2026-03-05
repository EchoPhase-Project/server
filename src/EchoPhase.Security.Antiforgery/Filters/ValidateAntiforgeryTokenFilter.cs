using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Security.Antiforgery.Filters
{
    public class ValidateAntiForgeryFilter : IAsyncActionFilter, IOrderedFilter
    {
        private readonly IAntiforgeryService _antiforgeryService;

        public int Order { get; set; } = int.MinValue;

        public ValidateAntiForgeryFilter(IAntiforgeryService antiforgeryService)
        {
            _antiforgeryService = antiforgeryService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            try
            {
                await _antiforgeryService.ValidateAsync();
            }
            catch (InvalidOperationException ex)
            {
                context.Result = new ObjectResult(new { error = ex.Message })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            await next();
        }
    }
}
