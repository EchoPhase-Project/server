// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Authentication.Cookies;

namespace EchoPhase.Security.Authentication.Extensions
{
    public static class CookieAuthenticationOptionsExtensions
    {
        public static void CopyFrom(this CookieAuthenticationOptions target, CookieAuthenticationOptions source)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (source == null) throw new ArgumentNullException(nameof(source));

            target.LoginPath = source.LoginPath;
            target.LogoutPath = source.LogoutPath;
            target.AccessDeniedPath = source.AccessDeniedPath;
            target.ExpireTimeSpan = source.ExpireTimeSpan;
            target.SlidingExpiration = source.SlidingExpiration;

            if (source.Cookie != null)
            {
                if (!string.IsNullOrEmpty(source.Cookie.Name))
                {
                    target.Cookie.Name = source.Cookie.Name;
                }

                target.Cookie.HttpOnly = source.Cookie.HttpOnly;
                target.Cookie.SameSite = source.Cookie.SameSite;
                target.Cookie.SecurePolicy = source.Cookie.SecurePolicy;

                // Path и Domain могут быть null, но лучше копировать, если заданы
                if (!string.IsNullOrEmpty(source.Cookie.Path))
                {
                    target.Cookie.Path = source.Cookie.Path;
                }
                if (!string.IsNullOrEmpty(source.Cookie.Domain))
                {
                    target.Cookie.Domain = source.Cookie.Domain;
                }
            }
        }
    }
}
