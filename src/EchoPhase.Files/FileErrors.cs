// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;

namespace EchoPhase.Files
{
    public static class FileErrors
    {
        public const string NotFound = ErrorCodes.NotFound;
        public const string Invalid = ErrorCodes.Invalid;
        /// <summary>Object storage is not set up on this deployment (503).</summary>
        public const string NotConfigured = ErrorCodes.NotConfigured;
        /// <summary>Over the limit of its purpose (413).</summary>
        public const string TooLarge = "FileTooLarge";
        /// <summary>Over the owner's total (413).</summary>
        public const string QuotaExceeded = "QuotaExceeded";
        /// <summary>A content its purpose does not take (415).</summary>
        public const string UnsupportedType = "UnsupportedType";
    }
}
