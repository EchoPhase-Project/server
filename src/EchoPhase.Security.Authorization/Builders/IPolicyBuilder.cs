// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Builders
{
    public interface IPolicyBuilder
    {
        AuthorizationPolicy? Build(string policyBody);
    }
}
