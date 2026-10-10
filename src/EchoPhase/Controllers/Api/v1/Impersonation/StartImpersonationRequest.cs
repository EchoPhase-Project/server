// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Impersonation
{
    /// <param name="Username">The user to act as.</param>
    /// <param name="Reason">Why; stored for audit and shown to the user.</param>
    /// <param name="Password">Your own password, to confirm.</param>
    public sealed record StartImpersonationRequest(
        [Required] string Username,
        [Required][MaxLength(256)] string Reason,
        [Required] string Password
    );
}
