// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;

namespace EchoPhase.Security.Authentication.Impersonation
{
    public static class ImpersonationErrors
    {
        public const string NotFound = ErrorCodes.NotFound;
        public const string Invalid = ErrorCodes.Invalid;
        public const string InvalidCredentials = ErrorCodes.InvalidCredentials;

        /// <summary>Missing permission, or the target ranks equal to or above the actor.</summary>
        public const string Forbidden = ErrorCodes.Forbidden;
    }

    /// <summary>
    /// The token is returned only here. Send it as <c>Authorization: Bearer</c>;
    /// it never replaces the actor's own session cookies.
    /// </summary>
    public sealed record ImpersonationStarted(
        Guid ImpersonationId,
        Guid TargetId,
        string? TargetUserName,
        string AccessToken,
        int ExpiresIn);

    /// <summary>What the target is told when someone starts acting as them.</summary>
    public sealed record ImpersonationNotice(
        Guid ImpersonationId,
        Guid ActorId,
        string? ActorUserName,
        string Reason,
        DateTime ExpiresAt);
}
