// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Connections;
using EchoPhase.Server.Controllers.Api;
using EchoPhase.Server.Controllers.Api.v1.Common;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Tests
{
    /// <summary>
    /// A refusal of Discord or Twitch reaches the caller as ErrorBody with the
    /// provider's status, except where that status would mislead: a success (the
    /// answer could not be read) and 401 (the caller would take it for the end of
    /// its own session).
    /// </summary>
    public class UpstreamErrorTests
    {
        private sealed class Probe : ApiControllerBase
        {
            public ObjectResult Of(string? message, int status) => UpstreamError(message, status);
        }

        [Theory]
        [InlineData(200, 502, ConnectionErrors.Upstream)]
        [InlineData(0, 502, ConnectionErrors.Upstream)]
        [InlineData(401, 409, ConnectionErrors.ReauthRequired)]
        [InlineData(404, 404, ConnectionErrors.Upstream)]
        [InlineData(429, 429, ConnectionErrors.Upstream)]
        public void UpstreamError_KeepsTheStatusUnlessItMisleads(int upstream, int status, string code)
        {
            var result = new Probe().Of("refused", upstream);

            Assert.Equal(status, result.StatusCode);
            var body = Assert.IsType<ErrorBody>(result.Value);
            Assert.Equal(code, body.Code);
            Assert.Equal("refused", body.Message);
        }

        [Fact]
        public void UpstreamError_HasAMessageWhenTheProviderGaveNone()
            => Assert.False(string.IsNullOrEmpty(Assert.IsType<ErrorBody>(new Probe().Of(null, 500).Value).Message));
    }
}
