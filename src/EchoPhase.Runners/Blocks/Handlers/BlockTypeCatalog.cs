// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace EchoPhase.Runners.Blocks.Handlers
{
    /// <summary>A block type as the editor sees it: what to draw and which form to show.</summary>
    public sealed record BlockTypeInfo(
        BlockTypes Type,
        int Version,
        string Description,
        bool IsTrigger,
        IReadOnlyList<string> Outputs,
        JsonNode ParamsSchema);

    /// <summary>
    /// Describes every registered block type, its params as JSON Schema
    /// (built from the params classes, so it cannot drift from what the
    /// runner accepts). Built once: types are fixed at startup.
    /// </summary>
    public sealed class BlockTypeCatalog
    {
        private readonly Lazy<IReadOnlyList<BlockTypeInfo>> _types;

        public BlockTypeCatalog(BlockTypeRegistry registry)
        {
            _types = new(() => Build(registry));
        }

        public IReadOnlyList<BlockTypeInfo> Types => _types.Value;

        private static IReadOnlyList<BlockTypeInfo> Build(BlockTypeRegistry registry)
        {
            var options = new JsonSerializerOptions(BlockParamsBinder.Options)
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            };
            var exporter = new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true };

            return registry.All
                .Select(d => new BlockTypeInfo(
                    d.Type, d.Version, d.Description, d.IsTrigger, d.Outputs,
                    options.GetJsonSchemaAsNode(d.ParamsType, exporter)))
                .ToList();
        }
    }
}
