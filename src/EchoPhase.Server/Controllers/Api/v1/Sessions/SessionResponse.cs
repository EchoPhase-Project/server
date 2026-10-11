// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Server.Controllers.Api.v1.Sessions
{
    /// <summary>A signed-in device. Secrets (the refresh token hash) never leave the server.</summary>
    public sealed record SessionResponse(Guid Id, Guid UserId, string? DeviceId)
    {
        public static SessionResponse From(Session session) => new(session.Id, session.UserId, session.DeviceId);
    }
}
