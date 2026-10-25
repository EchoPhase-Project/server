// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Projection.Tests
{
    internal static class Must
    {
        /// <summary>The value, after asserting it is there.</summary>
        public static T Present<T>(T? value) where T : class
        {
            Assert.NotNull(value);
            return value;
        }
    }
}
