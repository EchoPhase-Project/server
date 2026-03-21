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

namespace EchoPhase.Runners.Blocks.Handlers
{
    [BlockTypeHandler(BlockTypes.Set)]
    public class SetHandler : BlockHandlerBase<SetParams>
    {
        private readonly ILexer<TemplateToken> _lexer;
        private readonly IParser<TemplateToken> _parser;
        private readonly ILexer<PathToken> _pathLexer;
        private readonly IPathParser<PathToken> _pathParser;

        public SetHandler(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
            _lexer = GetService<ILexer<TemplateToken>>();
            _parser = GetService<IParser<TemplateToken>>();
            _pathLexer = GetService<ILexer<PathToken>>();
            _pathParser = GetService<IPathParser<PathToken>>();
        }

        public override Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, SetParams param)
        {
            object? value;

            switch (param.Value.ValueKind)
            {
                case JsonValueKind.String:
                    var raw = param.Value.GetString()
                        ?? throw new InvalidOperationException("Variable string parse exception.");
                    var result = Eval.Process<string>(_lexer, _parser, raw, context.Variables);
                    if (!result.TryGetValue(out var processed))
                        if (result.TryGetError(out var err))
                            throw new InvalidOperationException(err.Message);
                        else
                            throw new InvalidOperationException("Unknown error");
                    value = TryParsePrimitive(processed);
                    break;

                case JsonValueKind.Number:
                    value = param.Value.TryGetInt64(out long l) ? (object)l
                          : param.Value.GetDouble();
                    break;

                case JsonValueKind.True:
                    value = true;
                    break;

                case JsonValueKind.False:
                    value = false;
                    break;

                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    value = null;
                    break;

                case JsonValueKind.Object:
                    value = JsonElementToDictionary(param.Value);
                    break;

                case JsonValueKind.Array:
                    value = JsonElementToList(param.Value);
                    break;

                default:
                    value = param.Value.ToString();
                    break;
            }

            if (value != null)
                Eval.Set(_pathLexer, _pathParser, context.Variables, param.Name, value);

            return Task.FromResult(param.Next);
        }

        private static Dictionary<string, object?> JsonElementToDictionary(JsonElement element)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var prop in element.EnumerateObject())
                dict[prop.Name] = JsonElementToObject(prop.Value);
            return dict;
        }

        private static List<object?> JsonElementToList(JsonElement element)
        {
            var list = new List<object?>();
            foreach (var item in element.EnumerateArray())
                list.Add(JsonElementToObject(item));
            return list;
        }

        private static object? JsonElementToObject(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out long l) ? (object)l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => JsonElementToDictionary(element),
            JsonValueKind.Array => JsonElementToList(element),
            _ => element.GetRawText()
        };

        private object TryParsePrimitive(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            if (int.TryParse(input, out int i))
                return i;

            if (double.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d))
                return d;

            if (bool.TryParse(input, out bool b))
                return b;

            if (input.Trim().ToLowerInvariant() == "null")
                return null!;

            return input;
        }
    }
}
