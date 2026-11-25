// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Server.Services.Diagnostics;

namespace EchoPhase.Server.Tests
{
    public class ServerVersionTests
    {
        [Theory]
        [InlineData("0.9.0-12-g6c023e2", "0.9.0-12-g6c023e2")]
        [InlineData(" dev-0.6.0-dirty.20261125 ", "dev-0.6.0-dirty.20261125")]
        [InlineData("", "0.0.0-local")]
        [InlineData("  ", "0.0.0-local")]
        [InlineData(null, "0.0.0-local")]
        public void Of_TheImageTag_OrLocalWithoutOne(string? value, string version)
            => Assert.Equal(version, ServerVersion.Of(value));
    }
}
