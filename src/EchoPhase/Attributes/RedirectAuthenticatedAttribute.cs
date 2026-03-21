// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EchoPhase.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class RedirectAuthenticatedAttribute : Attribute, IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.User.Identity is null)
                return;

            var isAuthenticated = context.HttpContext.User.Identity.IsAuthenticated;

            if (isAuthenticated)
            {
                context.Result = new RedirectToActionResult("Index", "Index", null);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
