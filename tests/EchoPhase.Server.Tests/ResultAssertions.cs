// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;

namespace EchoPhase.Server.Tests
{
    public static class ResultAssertions
    {
        /// <summary>The value of a result that must have succeeded; fails the test with its error otherwise.</summary>
        public static T Ok<T>(this IServiceResult<T> result)
        {
            Assert.True(result.Successful, $"Expected success, got {result.Error}");
            return result.Value;
        }
    }
}
