// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Types.Extensions
{
    public static class EnumExtensions
    {
        public static bool TryParseEnum<T>(this string value, out T result) where T : struct
        {
            result = default(T);
            if (!typeof(T).IsEnum)
                return false;

            return Enum.TryParse(value, true, out result);
        }
    }
}
