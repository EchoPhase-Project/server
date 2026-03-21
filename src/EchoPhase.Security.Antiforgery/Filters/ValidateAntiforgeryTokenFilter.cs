// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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
