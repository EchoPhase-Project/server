// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;

namespace EchoPhase.Runners.Blocks.Handlers
{
    [BlockType(BlockTypes.Set, Description = "Sets a variable; its output is the value.")]
    public class SetHandler : BlockHandlerBase<SetParams>
    {
        public override Task<BlockResult> ExecuteAsync(BlockCall call, SetParams param)
        {
            var context = call.Context;
            object? value;

            switch (param.Value.ValueKind)
            {
                case JsonValueKind.String:
                    var raw = param.Value.GetString() ?? string.Empty;
                    // "@items" or "@(a + 1)" keeps its value's type (a list stays a
                    // list); mixed text is text, and plain text like "5" reads as 5.
                    var evaluated = Eval.Evaluate(raw, call.Scope);
                    value = evaluated is string text ? TryParsePrimitive(text) : evaluated;
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

                default:
                    value = Eval.Plain(param.Value);
                    break;
            }

            if (value != null)
                Eval.Set(context.Variables, param.Name, value);

            return Task.FromResult(BlockResult.Main(value));
        }

        private object TryParsePrimitive(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            if (long.TryParse(input, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long i))
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
