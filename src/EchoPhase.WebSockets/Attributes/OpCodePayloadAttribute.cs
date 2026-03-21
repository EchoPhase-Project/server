// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class OpCodePayloadAttribute : Attribute
    {
        public OpCodes OpCode
        {
            get;
        }

        public OpCodePayloadAttribute(OpCodes opCode)
        {
            OpCode = opCode;
        }
    }
}
