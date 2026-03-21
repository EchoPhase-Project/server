// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text;

namespace EchoPhase.Security.BitMasks.Extensions
{
    public static class StringExtensions
    {
        public static byte[] ComputeXxHash3(this string input)
        {
            return Encoding.UTF8.GetBytes(input).ComputeXxHash3();
        }
    }
}
