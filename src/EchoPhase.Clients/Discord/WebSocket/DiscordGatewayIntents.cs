// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Clients.Discord.WebSocket
{
    /// <summary>
    /// Gateway intents: which events Discord sends a connection. Privileged
    /// ones (members, message content) must also be enabled for the bot in
    /// the Discord developer portal, or Discord closes the connection (4014).
    /// </summary>
    public static class DiscordGatewayIntents
    {
        public const int Guilds = 1 << 0;
        public const int GuildMembers = 1 << 1;            // privileged
        public const int GuildMessages = 1 << 9;
        public const int GuildMessageReactions = 1 << 10;
        public const int DirectMessages = 1 << 12;
        public const int MessageContent = 1 << 15;         // privileged

        /// <summary>Guild and direct messages, without their text (no privileged intents).</summary>
        public const int Default = Guilds | GuildMessages | DirectMessages;

        /// <summary>Events a trigger can wait for, with the intents Discord needs to send them.</summary>
        public static readonly IReadOnlyDictionary<string, int> ForEvent = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["MESSAGE_CREATE"] = GuildMessages | DirectMessages | MessageContent,
            ["MESSAGE_UPDATE"] = GuildMessages | DirectMessages | MessageContent,
            ["MESSAGE_DELETE"] = GuildMessages | DirectMessages,
            ["MESSAGE_REACTION_ADD"] = GuildMessageReactions,
            ["MESSAGE_REACTION_REMOVE"] = GuildMessageReactions,
            ["GUILD_MEMBER_ADD"] = GuildMembers,
            ["GUILD_MEMBER_REMOVE"] = GuildMembers,
            ["GUILD_MEMBER_UPDATE"] = GuildMembers,
            ["GUILD_CREATE"] = Guilds,
            ["CHANNEL_CREATE"] = Guilds,
        };

        /// <summary>The intents covering every event in <paramref name="events"/> (Guilds always).</summary>
        public static int For(IEnumerable<string> events)
            => events.Aggregate(Guilds, (all, e) => all | ForEvent.GetValueOrDefault(e));
    }
}
