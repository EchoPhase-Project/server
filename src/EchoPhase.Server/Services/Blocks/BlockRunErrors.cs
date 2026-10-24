// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;

namespace EchoPhase.Server.Services.Blocks
{
    public static class BlockRunErrors
    {
        public const string NotFound = ErrorCodes.NotFound;
        public const string Invalid = ErrorCodes.Invalid;
        public const string TooManyActive = "TooManyActive";
        public const string QueueFull = "QueueFull";
        public const string AlreadyFinished = "AlreadyFinished";
        public const string NotFinished = "NotFinished";
    }
}
