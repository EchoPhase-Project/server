// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Server.Controllers.Api.v1.Users
{
    public sealed record GrantPermissionsRequest
    {
        [Required]
        [MinLength(1)]
        public string[] Permissions { get; set; } = [];

        [Required]
        public string Reason { get; set; } = string.Empty;

        /// <summary>Null: until revoked. With Z or an offset.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
