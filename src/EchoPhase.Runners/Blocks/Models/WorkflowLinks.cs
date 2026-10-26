// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using System.Text.Json.Nodes;
using EchoPhase.Runners.Blocks.Handlers;

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>What a program reaches outside itself.</summary>
    /// <param name="Workflows">The keys of the workflows of its package it calls (ExecuteWorkflow).</param>
    /// <param name="Connections">The owner's connections, by name.</param>
    public sealed record ProgramLinks(IReadOnlySet<Guid> Workflows, IReadOnlySet<string> Connections);

    /// <summary>How to point a program at another owner's things.</summary>
    /// <param name="Connections">Old connection name to new; names not in it stay.</param>
    /// <param name="WebhookSecret">Gives every WebhookTrigger a new secret; null: they keep theirs.</param>
    public sealed record LinkRewrite(
        IReadOnlyDictionary<string, string> Connections,
        Func<string>? WebhookSecret = null);

    /// <summary>
    /// The places a program names the workflows of its package and its
    /// owner's connections and secrets: read, and rewritten when a program
    /// moves to another owner (a published package installed). Workflows are
    /// named by key, the same in every copy, so they never need rewriting.
    /// </summary>
    public static class WorkflowLinks
    {
        public static ProgramLinks Of(BlockProgram program)
        {
            var workflows = new HashSet<Guid>();
            var connections = new HashSet<string>(StringComparer.Ordinal);
            foreach (var block in Blocks(Node(program)))
            {
                var parameters = block.Params;
                if (block.Type == BlockTypes.ExecuteWorkflow.ToString() && GuidOf(parameters?["workflow"]) is { } target)
                    workflows.Add(target);
                if (parameters?["connection"] is JsonValue connection && connection.TryGetValue(out string? name)
                    && !string.IsNullOrEmpty(name))
                    connections.Add(name);
            }

            return new ProgramLinks(workflows, connections);
        }

        public static BlockProgram Rewrite(BlockProgram program, LinkRewrite rewrite)
        {
            var root = Node(program);
            foreach (var block in Blocks(root))
            {
                if (block.Params is not { } parameters)
                    continue;
                if (parameters["connection"] is JsonValue connection && connection.TryGetValue(out string? name)
                    && rewrite.Connections.TryGetValue(name, out var newName))
                    parameters["connection"] = newName;
                if (block.Type == BlockTypes.WebhookTrigger.ToString() && rewrite.WebhookSecret is { } secret)
                    parameters["secret"] = secret();
            }

            return root.Deserialize<BlockProgram>(BlockParamsBinder.Options)
                ?? throw new InvalidOperationException("A rewritten program read back as nothing.");
        }

        private sealed record BlockNode(string? Type, JsonObject? Params);

        private static JsonObject Node(BlockProgram program)
            => JsonSerializer.SerializeToNode(program, BlockParamsBinder.Options) as JsonObject
                ?? throw new InvalidOperationException("A program did not serialize to an object.");

        private static IEnumerable<BlockNode> Blocks(JsonObject program)
            => (program["blocks"] as JsonArray ?? [])
                .OfType<JsonObject>()
                .Select(b => new BlockNode(
                    b["type"] is JsonValue type && type.TryGetValue(out string? name) ? name : null,
                    b["params"] as JsonObject));

        private static Guid? GuidOf(JsonNode? node)
            => node is JsonValue value && value.TryGetValue(out string? text) && Guid.TryParse(text, out var id) ? id : null;
    }
}
