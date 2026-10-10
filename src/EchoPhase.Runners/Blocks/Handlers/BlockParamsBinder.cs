// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using System.Text.Json.Serialization;
using EchoPhase.Configuration;

namespace EchoPhase.Runners.Blocks.Handlers
{
    /// <summary>Raw params (JSON from the program) to a handler's params type, validated.</summary>
    public static class BlockParamsBinder
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public static object Bind(Type paramsType, object? raw)
        {
            object? typed;
            try
            {
                typed = raw switch
                {
                    null => null,
                    _ when paramsType.IsInstanceOfType(raw) => raw,
                    JsonElement json => json.Deserialize(paramsType, Options),
                    _ => null
                };
            }
            catch (JsonException ex)
            {
                throw new InvalidCastException($"Invalid params: {ex.Message}", ex);
            }

            if (typed is null)
                throw new InvalidCastException($"Invalid params: expected {paramsType.Name}.");

            if (typed is IValidatable validatable && validatable.Validate() is { Successful: false } result)
                throw new InvalidOperationException(result.Error.Value);

            return typed;
        }
    }
}
