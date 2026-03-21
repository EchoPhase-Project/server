// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    public interface IEventMessage
    {
        OpCodes Op
        {
            get;
        }
    }

    public interface IEventMessage<T> : IEventMessage
    {
        T D
        {
            get;
        }
    }
}
