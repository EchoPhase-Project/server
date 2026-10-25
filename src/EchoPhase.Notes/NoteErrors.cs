// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;

namespace EchoPhase.Notes
{
    public static class NoteErrors
    {
        public const string NotFound = ErrorCodes.NotFound;
        public const string Invalid = ErrorCodes.Invalid;
        /// <summary>The note is shared with the caller, but not for this (403).</summary>
        public const string Forbidden = ErrorCodes.Forbidden;
        /// <summary>The note changed under the request (its reminder was sent) (409).</summary>
        public const string Conflict = ErrorCodes.Conflict;
    }
}
