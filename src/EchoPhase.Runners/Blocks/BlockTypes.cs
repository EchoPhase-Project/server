// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;

namespace EchoPhase.Runners.Blocks
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BlockTypes
    {
        Set,
        Print,
        If,
        For,
        While,
        DiscordGetUserGuilds
    }
}
