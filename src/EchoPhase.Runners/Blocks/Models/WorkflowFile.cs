// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using System.Text.Json.Nodes;
using EchoPhase.Runners.Blocks.Handlers;

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>A workflow read from an export file.</summary>
    /// <param name="DeclaredHash">The contentHash the file states, if any; compare with the program's own.</param>
    public sealed record WorkflowFileContent(string Name, BlockProgram Program, string? DeclaredHash)
    {
        /// <summary>The program is what the file says it is: no hash stated, or the same one.</summary>
        public bool Intact => DeclaredHash is null || DeclaredHash == BlockProgramHash.Of(Program);
    }

    /// <summary>
    /// The export file of a workflow, the same from the API and the CLI:
    /// { format, name, version, contentHash, exportedAt, program }. Files from
    /// before the format ({ name, program }) still import.
    /// </summary>
    public static class WorkflowFile
    {
        public const int Format = 1;

        public static string Write(string name, BlockProgram program, int? version, DateTimeOffset exportedAt)
        {
            var file = new JsonObject
            {
                ["format"] = Format,
                ["name"] = name,
                ["version"] = version,
                ["contentHash"] = BlockProgramHash.Of(program),
                ["exportedAt"] = exportedAt.ToUniversalTime().ToString("O"),
                ["program"] = JsonSerializer.SerializeToNode(program, BlockParamsBinder.Options),
            };
            return file.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        /// <exception cref="FormatException">Not a workflow file, or a newer format than this server reads.</exception>
        public static WorkflowFileContent Parse(string json)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException e)
            {
                throw new FormatException($"Not JSON: {e.Message}", e);
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String
                    || !root.TryGetProperty("program", out var program) || program.ValueKind != JsonValueKind.Object)
                    throw new FormatException("Expected a workflow export: { \"name\": \"...\", \"program\": { ... } }.");

                if (root.TryGetProperty("format", out var format)
                    && (format.ValueKind != JsonValueKind.Number || !format.TryGetInt32(out var number) || number > Format))
                    throw new FormatException($"This file is in a newer export format than this server reads ({Format}).");

                string? hash = root.TryGetProperty("contentHash", out var declared) && declared.ValueKind == JsonValueKind.String
                    ? declared.GetString()
                    : null;

                BlockProgram parsed;
                try
                {
                    parsed = program.Deserialize<BlockProgram>(BlockParamsBinder.Options) ?? new BlockProgram();
                }
                catch (JsonException e)
                {
                    throw new FormatException($"The program is not valid: {e.Message}", e);
                }

                return new WorkflowFileContent(
                    name.GetString() ?? throw new FormatException("The workflow's name is null."), parsed, hash);
            }
        }
    }
}
