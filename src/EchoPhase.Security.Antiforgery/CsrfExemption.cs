// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Http;

namespace EchoPhase.Security.Antiforgery
{
    /// <summary>
    /// CSRF only threatens ambient credentials (cookies). A request that was
    /// authenticated by an explicit <c>Authorization</c> header cannot be forged
    /// cross-site, so the authentication handler marks it and the antiforgery
    /// check is skipped. Requests that fell back to cookies are never marked.
    /// </summary>
    public static class CsrfExemption
    {
        private static readonly object ItemKey = new();

        public static void MarkHeaderAuthenticated(HttpContext context)
            => context.Items[ItemKey] = true;

        public static bool IsHeaderAuthenticated(HttpContext context)
            => context.Items.TryGetValue(ItemKey, out var value) && value is true;
    }
}
