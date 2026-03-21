// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Extensions
{
    public static class EnumExtensions
    {
        public static bool IsIgnored(this OpCodes opCode)
        {
            var memberInfo = typeof(OpCodes).GetMember(opCode.ToString());
            if (memberInfo.Length > 0)
            {
                return memberInfo[0].GetCustomAttributes(typeof(IgnoreOpCodeAttribute), false).Any();
            }
            return false;
        }
    }
}
