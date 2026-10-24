// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Server.Extensions;
using Microsoft.Extensions.Logging;
using Sentry.AspNetCore;
using Sentry.Extensibility;

namespace EchoPhase.Server.Tests
{
    public class ErrorReportingTests
    {
        [Fact]
        public void ErrorsOnlyWithoutPersonalDataOrTraces()
        {
            var options = new SentryAspNetCoreOptions();
            ErrorReporting.Configure(options);

            Assert.False(options.SendDefaultPii);
            Assert.Equal(RequestSize.None, options.MaxRequestBodySize);
            Assert.Equal(0, options.TracesSampleRate);
            Assert.Equal(LogLevel.Error, options.MinimumEventLevel);
            Assert.False(string.IsNullOrEmpty(options.Release));
            // No DSN here: the SDK stays off until SENTRY_DSN is set.
            Assert.Null(options.Dsn);
        }
    }
}
