using EchoPhase.Attributes;
using EchoPhase.Helpers;
using EchoPhase.Interfaces;
using EchoPhase.Runners.Enums;
using EchoPhase.Runners.Params;
using Newtonsoft.Json.Linq;

namespace EchoPhase.Runners.Handlers
{
    [BlockTypeHandler(BlockTypes.Set)]
    public class SetHandler : BlockHandlerBase<SetParams>
    {
        public SetHandler(IServiceProvider serviceProvider)
            : base(serviceProvider) { }

        public override Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, SetParams param)
        {
            object? value;

            if (param.Value.Type == JTokenType.String)
            {
                string? raw = param.Value.Value<string>();

                if (raw == null)
                    throw new InvalidOperationException("Variable string parse exception.");

                string processed = ExpressionHelper.ProcessStringWithExpressions(raw, context.Variables);

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
                context.Variables[param.Name] = value;

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
