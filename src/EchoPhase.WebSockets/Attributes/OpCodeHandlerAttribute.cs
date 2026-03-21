// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class OpCodeHandlerAttribute : Attribute
    {
        public OpCodes OpCode
        {
            get;
        }

        public OpCodeHandlerAttribute(OpCodes opCode)
        {
            OpCode = opCode;
        }
    }
}
