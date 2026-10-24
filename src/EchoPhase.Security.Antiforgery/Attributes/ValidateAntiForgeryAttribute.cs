// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Antiforgery.Filters;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Security.Antiforgery.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class ValidateAntiForgeryAttribute : Attribute, IFilterFactory, IOrderedFilter
    {
        public int Order { get; set; } = int.MinValue;
        public bool IsReusable => false;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<ValidateAntiForgeryFilter>();
        }
    }
}
