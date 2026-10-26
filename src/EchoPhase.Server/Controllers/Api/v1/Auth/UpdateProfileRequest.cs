// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Server.Controllers.Api.v1.Auth
{
    /// <summary>Profile fields to change; an omitted (null) field stays as it is.</summary>
    /// <param name="Name">Display name, at least 2 characters.</param>
    /// <param name="TimeZone">IANA time zone, e.g. "Europe/Moscow".</param>
    /// <param name="Username">A new username, checked as at registration; the old one becomes free.</param>
    public sealed record UpdateProfileRequest(
        [MinLength(2)] string? Name = null,
        [MaxLength(64)] string? TimeZone = null,
        [MaxLength(64)] string? Username = null
    );

    /// <param name="CurrentPassword">The password now: proves it is the account's owner at the keyboard.</param>
    /// <param name="NewPassword">The new one, held to the same rules as at registration.</param>
    public sealed record ChangePasswordRequest(
        [Required] string CurrentPassword,
        [Required] string NewPassword
    );
}
