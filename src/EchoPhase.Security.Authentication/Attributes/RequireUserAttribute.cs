// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Authentication.Filters;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Security.Authentication.Attributes
{
    public class RequireUserAttribute : TypeFilterAttribute
    {
        public RequireUserAttribute() : base(typeof(RequireUserFilter)) { }
    }
}
