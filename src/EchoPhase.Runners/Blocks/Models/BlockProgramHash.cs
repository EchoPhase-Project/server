// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using EchoPhase.Runners.Blocks.Handlers;

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>
    /// What a program is, as a hash: SHA-256 (hex) of its canonical JSON, the
    /// keys of every object sorted, no whitespace. Two programs with the same
    /// blocks, connections and settings have the same hash however they were
    /// written; a workflow's version and a run name the program they hold.
    /// </summary>
    public static class BlockProgramHash
    {
        public static string Of(BlockProgram program)
            => Of(JsonSerializer.SerializeToNode(program, BlockParamsBinder.Options));

        /// <summary>The hash of a program stored as JSON text.</summary>
        public static string OfJson(string json) => Of(JsonNode.Parse(json));

        private static string Of(JsonNode? node)
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
                WriteCanonical(writer, node);
            return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
        }

        private static void WriteCanonical(Utf8JsonWriter writer, JsonNode? node)
        {
            switch (node)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case JsonObject obj:
                    writer.WriteStartObject();
                    foreach (var (name, value) in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(name);
                        WriteCanonical(writer, value);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonArray array:
                    writer.WriteStartArray();
                    foreach (var item in array)
                        WriteCanonical(writer, item);
                    writer.WriteEndArray();
                    break;
                default:
                    node.WriteTo(writer);
                    break;
            }
        }
    }
}
