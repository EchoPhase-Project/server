// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;
using EchoPhase.Types.Attributes;

namespace EchoPhase.Runners.Blocks
{
    /// <summary>The kinds of blocks; more come with new versions, so clients must expect unknown ones.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [ExtensibleEnum]
    public enum BlockTypes
    {
        Set,
        Print,
        If,
        For,
        While,
        Merge,
        ManualTrigger,
        WebhookTrigger,
        ScheduleTrigger,
        DiscordEventTrigger,
        DiscordGetUserGuilds,
        TwitchGetVips,
        StopAndError,
        ErrorTrigger,
        Switch,
        ExecuteWorkflow,
        HttpRequest,
        RespondToWebhook,
        Storage
    }
}
