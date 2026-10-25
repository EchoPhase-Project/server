// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Discord;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;

namespace EchoPhase.Runners.Blocks.Handlers
{
    [BlockType(BlockTypes.DiscordGetUserGuilds, Description = "The user's Discord guilds through a stored connection.")]
    public class DiscordGetUserGuildsHandler : BlockHandlerBase<DiscordGetUserGuildsParams>
    {
        private readonly IDiscordClient _discordClient;

        public DiscordGetUserGuildsHandler(IDiscordClient discordClient)
        {
            _discordClient = discordClient;
        }

        public override async Task<BlockResult> ExecuteAsync(BlockCall call, DiscordGetUserGuildsParams param)
        {
            var context = call.Context;

            // Credentials (and their refresh) are handled by the client's auth handler.
            var result = await _discordClient.GetUserGuildsAsync(
                param.Query, new ConnectionRef(context.UserId, param.Connection), context.CancellationToken);

            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Error.Message);

            var value = result.Value
                .Select(item => ToVariable(item))
                .ToList();

            if (!string.IsNullOrWhiteSpace(param.Name))
                Eval.Set(context.Variables, param.Name, value);

            return BlockResult.Main(value);
        }
    }
}
