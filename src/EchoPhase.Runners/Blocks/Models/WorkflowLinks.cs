// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using System.Text.Json.Nodes;
using EchoPhase.Runners.Blocks.Handlers;

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>What a program reaches outside itself.</summary>
    /// <param name="Workflows">The owner's other workflows: ExecuteWorkflow targets and the error workflow.</param>
    /// <param name="Connections">The owner's connections, by name.</param>
    public sealed record ProgramLinks(IReadOnlySet<Guid> Workflows, IReadOnlySet<string> Connections);

    /// <summary>How to point a program at another owner's things.</summary>
    /// <param name="Workflows">Old workflow id to new; ids not in it stay.</param>
    /// <param name="Connections">Old connection name to new; names not in it stay.</param>
    /// <param name="WebhookSecret">Gives every WebhookTrigger a new secret; null: they keep theirs.</param>
    public sealed record LinkRewrite(
        IReadOnlyDictionary<Guid, Guid> Workflows,
        IReadOnlyDictionary<string, string> Connections,
        Func<string>? WebhookSecret = null);

    /// <summary>
    /// The places a program names its owner's workflows, connections and
    /// secrets, read and rewritten: what changes when a program moves to
    /// another owner (a published package installed).
    /// </summary>
    public static class WorkflowLinks
    {
        public static ProgramLinks Of(BlockProgram program)
        {
            var workflows = new HashSet<Guid>();
            var connections = new HashSet<string>(StringComparer.Ordinal);
            if (program.Settings.ErrorWorkflowId is { } errorWorkflow)
                workflows.Add(errorWorkflow);

            foreach (var block in Blocks(Node(program)))
            {
                var parameters = block.Params;
                if (block.Type == BlockTypes.ExecuteWorkflow.ToString() && GuidOf(parameters?["workflowId"]) is { } target)
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
            if (root["settings"] is JsonObject settings && GuidOf(settings["errorWorkflowId"]) is { } errorWorkflow
                && rewrite.Workflows.TryGetValue(errorWorkflow, out var newErrorWorkflow))
                settings["errorWorkflowId"] = newErrorWorkflow;

            foreach (var block in Blocks(root))
            {
                if (block.Params is not { } parameters)
                    continue;
                if (block.Type == BlockTypes.ExecuteWorkflow.ToString() && GuidOf(parameters["workflowId"]) is { } target
                    && rewrite.Workflows.TryGetValue(target, out var newTarget))
                    parameters["workflowId"] = newTarget;
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
