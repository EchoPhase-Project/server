// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Discord.WebSocket;

namespace EchoPhase.Runners.Tests
{
    public class DiscordGatewayCloseCodesTests
    {
        [Theory]
        [InlineData(null, DiscordGatewayRecovery.Resume)]        // network drop
        [InlineData(1006, DiscordGatewayRecovery.Resume)]
        [InlineData(4000, DiscordGatewayRecovery.Resume)]
        [InlineData(4008, DiscordGatewayRecovery.Resume)]        // rate limited
        [InlineData(1000, DiscordGatewayRecovery.Reidentify)]    // Discord ended the session
        [InlineData(4007, DiscordGatewayRecovery.Reidentify)]    // bad sequence on resume
        [InlineData(4009, DiscordGatewayRecovery.Reidentify)]    // session timed out
        [InlineData(4004, DiscordGatewayRecovery.Fatal)]         // bad token
        [InlineData(4013, DiscordGatewayRecovery.Fatal)]
        [InlineData(4014, DiscordGatewayRecovery.Fatal)]         // privileged intents not enabled
        public void Classify_DecidesWhatComesNext(int? code, DiscordGatewayRecovery expected)
        {
            Assert.Equal(expected, DiscordGatewayCloseCodes.Classify(code).Recovery);
        }

        [Fact]
        public void DisallowedIntents_SaysWhereToFixIt()
        {
            Assert.Contains("Developer Portal", DiscordGatewayCloseCodes.Classify(4014).Message);
        }

        [Fact]
        public void Intents_CoverTheTriggerEvents()
        {
            var intents = DiscordGatewayIntents.For(["MESSAGE_CREATE", "GUILD_MEMBER_ADD"]);

            Assert.NotEqual(0, intents & DiscordGatewayIntents.MessageContent);
            Assert.NotEqual(0, intents & DiscordGatewayIntents.GuildMembers);
            Assert.NotEqual(0, intents & DiscordGatewayIntents.Guilds);
        }
    }
}
