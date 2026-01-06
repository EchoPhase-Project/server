using EchoPhase.Attributes;
using EchoPhase.Interfaces;
using EchoPhase.Runners.Enums;
using EchoPhase.Runners.Params;
using Newtonsoft.Json.Linq;
using EchoPhase.Expressions.Tokens;
using EchoPhase.Expressions;
using EchoPhase.Extensions;
using System.Collections;

namespace EchoPhase.Runners.Handlers
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

            if (param.Value.Type == JTokenType.String)
            {
                string? raw = param.Value.Value<string>();

                if (raw == null)
                    throw new InvalidOperationException("Variable string parse exception.");

                var result = Eval.Process<string>(_lexer, _parser, raw, context.Variables);

                if (!result.TryGetValue(out var processed))
                    if (result.TryGetError(out var err))
                        throw new Exception(err.Message);
                    else
                        throw new Exception("Unknown error");

                value = TryParsePrimitive(processed);
            }
            else if (param.Value is JValue jValue)
            {
                value = jValue.Value;
            }
            else
            {
                value = param.Value.Type switch
                {
                    JTokenType.Object => param.Value,
                    JTokenType.Array => param.Value,
                    _ => param.Value.ToString()
                };
            }

            if (value != null)
                Eval.Set(_pathLexer, _pathParser, context.Variables, param.Name, value);

            return Task.FromResult(param.Next);
        }


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
