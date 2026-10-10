// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;
using EchoPhase.Clients.Discord.WebSocket;
using EchoPhase.Configuration;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    /// <summary>Started from the API or the editor: the run's input is what the caller sends.</summary>
    public class ManualTriggerParams : IValidatable
    {
        public IValidationResult Validate() => ValidationResult.Success();
    }

    /// <summary>
    /// Started by an HTTP POST to the workflow's webhook URL with this secret
    /// (X-Webhook-Secret header); the run's input is the request body.
    /// </summary>
    public class WebhookTriggerParams : IValidatable
    {
        public const int MinSecretLength = 16;

        [JsonPropertyName("secret")]
        public string Secret { get; set; } = string.Empty;

        public IValidationResult Validate()
        {
            if (Secret.Length < MinSecretLength)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Secret), $"Secret must be at least {MinSecretLength} characters."));

            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// Started on a cron schedule in the workflow's time zone (DST handled:
    /// a skipped local time runs once at the next valid moment, a repeated one
    /// once). The run's input is { "scheduledAt": ISO time }.
    /// </summary>
    public class ScheduleTriggerParams : IValidatable
    {
        /// <summary>Five fields (minute hour day month weekday), e.g. "0 9 * * 1-5".</summary>
        [JsonPropertyName("cron")]
        public string Cron { get; set; } = string.Empty;

        /// <summary>Checked by the scheduler, which parses it; here only for presence.</summary>
        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Cron))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Cron), "Cron cannot be empty."));

            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// Started by a Discord gateway event on one of the owner's bot
    /// connections (opened by the server while the workflow is active). The
    /// run's input is { event, connection, data } with data as Discord sent it.
    /// </summary>
    public class DiscordEventTriggerParams : IValidatable
    {
        /// <summary>The owner's Discord bot connection.</summary>
        [JsonPropertyName("connection")]
        public string Connection { get; set; } = string.Empty;

        /// <summary>Gateway event names, e.g. MESSAGE_CREATE, GUILD_MEMBER_ADD.</summary>
        [JsonPropertyName("events")]
        public string[] Events { get; set; } = [];

        /// <summary>Only events from this guild (server).</summary>
        [JsonPropertyName("guildId")]
        public string? GuildId { get; set; }

        /// <summary>Only events from this channel.</summary>
        [JsonPropertyName("channelId")]
        public string? ChannelId { get; set; }

        /// <summary>Skip events caused by bots, the workflow's own bot included (no reply loops).</summary>
        [JsonPropertyName("ignoreBots")]
        public bool IgnoreBots { get; set; } = true;

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Connection))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Connection), "Connection cannot be empty."));

            if (Events.Length == 0)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Events), "At least one event is required."));

            var unknown = Events.Where(e => !DiscordGatewayIntents.ForEvent.ContainsKey(e)).ToList();
            if (unknown.Count > 0)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Events), $"Unknown events: {string.Join(", ", unknown)}. Known: {string.Join(", ", DiscordGatewayIntents.ForEvent.Keys)}."));

            return ValidationResult.Success();
        }
    }
}
