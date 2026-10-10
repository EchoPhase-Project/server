// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    public static class ErrorMessages
    {
        public static EventMessage<ErrorPayload> Create(ErrorCodes code, string message)
            => EventMessage<ErrorPayload>.Create(OpCodes.Error, e =>
            {
                e.Code = code;
                e.Message = message;
            });
    }
}
