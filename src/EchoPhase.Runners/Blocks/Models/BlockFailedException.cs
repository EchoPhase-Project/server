// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>
    /// A block failing on purpose (StopAndError): recorded with
    /// <see cref="ErrorType"/> as its type, and never retried.
    /// </summary>
    public sealed class BlockFailedException(string errorType, string message) : Exception(message)
    {
        public string ErrorType { get; } = errorType;
    }
}
