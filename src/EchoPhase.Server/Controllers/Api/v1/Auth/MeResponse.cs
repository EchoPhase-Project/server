// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Server.Controllers.Api.v1.Auth
{
    /// <param name="Id">The user's id.</param>
    /// <param name="UserName">The name to sign in with.</param>
    /// <param name="Name">The display name.</param>
    /// <param name="TimeZone">IANA time zone the user's times are shown in.</param>
    /// <param name="AvatarFileId">The user's picture, a file to fetch from /api/v1/files/{id}/content; null: none.</param>
    public sealed record MeResponse(Guid Id, string? UserName, string Name, string TimeZone, Guid? AvatarFileId)
    {
        public static MeResponse From(User user) => new(user.Id, user.UserName, user.Name, user.TimeZone, user.AvatarFileId);
    }
}
