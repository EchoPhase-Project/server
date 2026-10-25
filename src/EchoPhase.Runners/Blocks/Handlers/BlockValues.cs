// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Scripting;

namespace EchoPhase.Runners.Blocks.Handlers
{
    /// <summary>
    /// A param that is any JSON value: a string is a template ("@items" keeps
    /// the list, "Hi @name" is text), anything else is taken as written.
    /// </summary>
    public static class BlockValues
    {
        public static object? Evaluate(JsonElement value, IDictionary<string, object?> scope) => value.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            JsonValueKind.String => Eval.Evaluate(value.GetString() ?? string.Empty, scope),
            _ => Eval.Plain(value)
        };

        /// <summary>Why a template param does not parse, or null; for the validator.</summary>
        public static string? Check(JsonElement value)
            => value.ValueKind == JsonValueKind.String ? Eval.CheckTemplate(value.GetString() ?? string.Empty) : null;
    }
}
