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
            ErrorReporting.Configure(options, dsn: null);

            Assert.False(options.SendDefaultPii);
            Assert.Equal(RequestSize.None, options.MaxRequestBodySize);
            Assert.Equal(0, options.TracesSampleRate);
            Assert.Equal(LogLevel.Error, options.MinimumEventLevel);
            Assert.False(string.IsNullOrEmpty(options.Release));
            // No DSN: empty (off), not null (UseSentry throws on that).
            Assert.Equal(string.Empty, options.Dsn);
        }

        [Fact]
        public void TakesTheDsnFromTheEnvironment()
        {
            var options = new SentryAspNetCoreOptions();
            ErrorReporting.Configure(options, " http://key@glitchtip/1 ");
            Assert.Equal("http://key@glitchtip/1", options.Dsn);
        }

        /// <summary>
        /// The host builds as the OpenAPI export builds it (no databases): a
        /// service that fails at startup fails here, not in the release.
        /// </summary>
        [Fact]
        public void TheHostBuilds()
        {
            using var host = SwaggerHostFactory.CreateHost();
            Assert.NotNull(host.Services);
        }
    }
}
