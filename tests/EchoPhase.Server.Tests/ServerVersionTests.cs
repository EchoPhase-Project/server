// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Server.Services.Diagnostics;

namespace EchoPhase.Server.Tests
{
    /// <summary>The contract series a version belongs to.</summary>
    public class ServerVersionTests
    {
        [Theory]
        [InlineData("0.6.0", "0.6")]
        [InlineData("0.6.0-3-gabc1234", "0.6")]
        [InlineData("1.12.4-dirty.20261026T073219", "1.12")]
        [InlineData("dev-0.6.0-4-gc319a8c-dirty.20261025T175951", "0.6")]
        public void ReleasedBuilds_NameTheirSeries(string version, string series)
            => Assert.Equal(series, ServerVersion.SeriesOf(version));

        [Theory]
        [InlineData("0.0.0-local")]
        [InlineData("0.0.0-g938156d")]
        [InlineData("not a version")]
        public void UnreleasedBuilds_HaveNone(string version)
            => Assert.Null(ServerVersion.SeriesOf(version));

        [Fact]
        public void ATestBuild_IsLocal()
        {
            Assert.Equal("0.0.0-local", ServerVersion.Current);
            Assert.Null(ServerVersion.Api);
        }
    }
}
