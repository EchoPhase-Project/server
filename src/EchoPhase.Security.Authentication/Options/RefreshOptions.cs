// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EchoPhase.Security.Authentication.Options
{
    public class RefreshOptions : JwtBearerOptions
    {
        public new JwtBearerEvents Events { get; set; } = new();
    }
}
