// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Controllers.Api.v1.Auth
{
    public sealed record MeResponse(Guid Id, string? UserName, string Name, string TimeZone)
    {
        public static MeResponse From(User user) => new(user.Id, user.UserName, user.Name, user.TimeZone);
    }
}
