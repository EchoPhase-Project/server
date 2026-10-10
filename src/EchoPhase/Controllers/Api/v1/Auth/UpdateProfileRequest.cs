// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Auth
{
    /// <summary>Profile fields to change; an omitted (null) field stays as it is.</summary>
    /// <param name="Name">Display name, at least 2 characters.</param>
    /// <param name="TimeZone">IANA time zone, e.g. "Europe/Moscow".</param>
    public sealed record UpdateProfileRequest(
        [MinLength(2)] string? Name = null,
        [MaxLength(64)] string? TimeZone = null
    );
}
