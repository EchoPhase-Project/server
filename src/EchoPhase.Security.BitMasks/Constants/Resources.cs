// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Security.BitMasks.Constants
{
    /// <summary>
    /// What a permission applies to; one entry per API area.
    /// </summary>
    public class Resources : ConstantsBase<Resources>
    {
        public const string Users = "users";
        public const string Roles = "roles";
        public const string Sessions = "sessions";
        public const string ApiClients = "apiClients";
        public const string Connections = "connections";
        public const string OAuthApps = "oauthApps";
        /// <summary>Block programs run directly (api/v1/blocks/runs).</summary>
        public const string Scripts = "scripts";

        /// <summary>Saved block programs and their versions (api/v1/workflows).</summary>
        public const string Workflows = "workflows";
        public const string Discord = "discord";
        public const string Twitch = "twitch";

        /// <summary>Raw WebSocket gateway (<c>/gateway</c>).</summary>
        public const string Gateway = "gateway";
    }
}
