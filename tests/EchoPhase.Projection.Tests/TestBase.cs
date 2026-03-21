// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Projection.Tests
{
    public abstract class TestBase
    {
        protected static Dictionary<string, object?> AsDictionary(object? result) =>
            Assert.IsType<Dictionary<string, object?>>(result);

        protected static List<object?> AsList(object? result) =>
            Assert.IsType<List<object?>>(result);
    }
}
