// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Clients.Discord.WebSocket
{
    /// <summary>What to do after the gateway closed.</summary>
    public enum DiscordGatewayRecovery
    {
        /// <summary>Reconnect and resume the session: missed events are replayed.</summary>
        Resume,

        /// <summary>Reconnect with a new session (Identify); events in between are lost.</summary>
        Reidentify,

        /// <summary>Retrying cannot help (bad token, intents not allowed...): stop.</summary>
        Fatal
    }

    /// <summary>Discord's gateway close codes, with what they mean for the connection.</summary>
    public static class DiscordGatewayCloseCodes
    {
        public const int AuthenticationFailed = 4004;
        public const int InvalidIntents = 4013;
        public const int DisallowedIntents = 4014;

        public static (DiscordGatewayRecovery Recovery, string Message) Classify(int? code) => code switch
        {
            4000 => (DiscordGatewayRecovery.Resume, "Unknown error"),
            4001 => (DiscordGatewayRecovery.Resume, "Unknown opcode sent"),
            4002 => (DiscordGatewayRecovery.Resume, "Payload could not be decoded"),
            4003 => (DiscordGatewayRecovery.Reidentify, "Payload sent before identifying"),
            AuthenticationFailed => (DiscordGatewayRecovery.Fatal, "Authentication failed: the bot token is invalid"),
            4005 => (DiscordGatewayRecovery.Reidentify, "Identified more than once"),
            4007 => (DiscordGatewayRecovery.Reidentify, "Invalid sequence number on resume"),
            4008 => (DiscordGatewayRecovery.Resume, "Rate limited: too many payloads"),
            4009 => (DiscordGatewayRecovery.Reidentify, "Session timed out"),
            4010 => (DiscordGatewayRecovery.Fatal, "Invalid shard"),
            4011 => (DiscordGatewayRecovery.Fatal, "Sharding required: the bot is in too many guilds for one connection"),
            4012 => (DiscordGatewayRecovery.Fatal, "Invalid gateway API version"),
            InvalidIntents => (DiscordGatewayRecovery.Fatal, "Invalid intents"),
            DisallowedIntents => (DiscordGatewayRecovery.Fatal,
                "Disallowed intents: enable Message Content and/or Server Members for the bot in the Discord Developer Portal (Bot > Privileged Gateway Intents)"),
            // A normal close from Discord ends the session; anything else
            // (network drop, 1006) keeps it resumable.
            1000 or 1001 => (DiscordGatewayRecovery.Reidentify, "Session closed by Discord"),
            null => (DiscordGatewayRecovery.Resume, "Connection lost"),
            _ => (DiscordGatewayRecovery.Resume, $"Closed with code {code}")
        };
    }

    /// <summary>A gateway connection that stopped for good.</summary>
    /// <param name="CloseCode">Discord's close code, when it closed the connection.</param>
    /// <param name="Message">Why, for people.</param>
    public sealed record DiscordGatewayFailure(
        Guid UserId, string TokenName, Guid ConnectionId, int? CloseCode, string Message)
    {
        public bool AuthenticationFailed => CloseCode == DiscordGatewayCloseCodes.AuthenticationFailed;
    }

    /// <summary>Told when a gateway connection stops on a fatal error (registered with AddObserver).</summary>
    public interface IDiscordGatewayObserver
    {
        Task OnFailedAsync(DiscordGatewayFailure failure, CancellationToken ct = default);
    }
}
