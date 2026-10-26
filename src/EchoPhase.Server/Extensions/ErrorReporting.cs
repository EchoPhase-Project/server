// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Server.Services.Diagnostics;
using Sentry.AspNetCore;

namespace EchoPhase.Server.Extensions
{
    /// <summary>
    /// Errors to a Sentry-compatible service (GlitchTip) at SENTRY_DSN; off
    /// without one. Unhandled exceptions and error logs become events, with
    /// the logs before them as breadcrumbs. No traces (OpenTelemetry has
    /// them) and no personal data: no user, IP, cookies or request bodies.
    /// </summary>
    public static class ErrorReporting
    {
        public static void Configure(SentryAspNetCoreOptions options)
            => Configure(options, Environment.GetEnvironmentVariable("SENTRY_DSN"));

        public static void Configure(SentryAspNetCoreOptions options, string? dsn)
        {
            // UseSentry throws on a missing DSN; an empty one turns it off.
            if (string.IsNullOrWhiteSpace(options.Dsn))
                options.Dsn = dsn?.Trim() ?? string.Empty;
            options.Release = ServerVersion.Current;
            options.TracesSampleRate = 0;
            options.SendDefaultPii = false;
            options.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
            options.MinimumEventLevel = LogLevel.Error;
            options.MinimumBreadcrumbLevel = LogLevel.Information;
        }
    }
}
