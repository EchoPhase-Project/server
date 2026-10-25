// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Twitch;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;

namespace EchoPhase.Runners.Blocks.Handlers
{
    [BlockType(BlockTypes.TwitchGetVips, Description = "A channel's VIPs through a stored connection.")]
    public class TwitchGetVipsHandler : BlockHandlerBase<TwitchGetVipsParams>
    {
        private readonly ITwitchClient _twitchClient;

        public TwitchGetVipsHandler(ITwitchClient twitchClient)
        {
            _twitchClient = twitchClient;
        }

        public override async Task<BlockResult> ExecuteAsync(BlockCall call, TwitchGetVipsParams param)
        {
            var context = call.Context;

            // Credentials (and their refresh) are handled by the client's auth handler.
            var result = await _twitchClient.GetVipsAsync(
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
