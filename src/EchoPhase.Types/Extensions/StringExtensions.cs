// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;

namespace EchoPhase.Types.Extensions
{
    public static class StringExtensions
    {
        public static BitArray ToBitArray(this string base64) =>
            new BitArray(Convert.FromBase64String(base64));
    }
}
