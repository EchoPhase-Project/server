// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;
using EchoPhase.Scripting.Lexers;
using EchoPhase.Scripting.Parsers;
using EchoPhase.Scripting.Tokens;
using EchoPhase.Types.Result.Extensions;
using EchoPhase.Clients;
using EchoPhase.Clients.Discord;
using EchoPhase.Clients.Providers;
using System.Text.Json.Serialization;
using System.Reflection;
using System.Text;

namespace EchoPhase.Runners.Blocks.Handlers
{
    [BlockTypeHandler(BlockTypes.DiscordGetUserGuilds)]
    public class DiscordGetUserGuildsHandler : BlockHandlerBase<DiscordGetUserGuildsParams>
    {
        private readonly IDiscordClient _discordClient;
        private readonly IExternalTokenService _tokenService;
        private readonly ILexer<PathToken> _pathLexer;
        private readonly IPathParser<PathToken> _pathParser;

        public DiscordGetUserGuildsHandler(
            IDiscordClient discordClient,
            IExternalTokenService tokenService,
            ILexer<PathToken> pathLexer,
            IPathParser<PathToken> pathParser)
        {
            _discordClient = discordClient;
            _tokenService = tokenService;
            _pathLexer = pathLexer;
            _pathParser = pathParser;
        }

        public override async Task<IEnumerable<int>> ExecuteAsync(
            IBlockExecutionContext context,
            IBlock block,
            DiscordGetUserGuildsParams param)
        {
            var tokenResult = await _tokenService.GetAsync(
                context.UserId,
                DiscordTokenProvider.ProviderName,
                param.TokenName);

            if (!tokenResult.Successful)
                throw new InvalidOperationException(
                    $"Token '{DiscordTokenProvider.ProviderName}:{param.TokenName}' not found.");

            var botToken = Encoding.UTF8.GetString(tokenResult.Value!);

            var result = await _discordClient.GetUserGuildsAsync(param.Query, botToken);

            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Error!.Message);

            var value = result.Value!
                .Select(g => ToVariable(g))
                .ToList();

            Eval.Set(_pathLexer, _pathParser, context.Variables, param.Name, value);

            return param.Next;
        }

        private static object? ToVariable(object? obj)
        {
            if (obj is null)
                return null;

            var type = obj.GetType();

            if (type.IsPrimitive || obj is string || obj is decimal)
                return obj;

            if (obj is System.Collections.IEnumerable enumerable)
                return enumerable
                    .Cast<object?>()
                    .Select(ToVariable)
                    .ToList();

            if (type.IsClass)
                return type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .ToDictionary(
                        prop => prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name,
                        prop => ToVariable(prop.GetValue(obj))
                    );

            return obj;
        }
    }
}
