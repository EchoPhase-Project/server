// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Runners.Blocks.Handlers;

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>One workflow of a package version.</summary>
    /// <param name="Key">
    /// The workflow's key in the package: the same in every version and every
    /// installed copy, so an update finds the copy it replaces; the package's
    /// programs call each other by it.
    /// </param>
    /// <param name="Main">The one to open first.</param>
    /// <param name="ContentHash">Its program's hash, as published (webhook secrets removed).</param>
    public sealed record PackageWorkflow(Guid Key, string Name, bool Main, string ContentHash, BlockProgram Program);

    /// <summary>A connection the package's programs use: the installer names one of theirs for it.</summary>
    /// <param name="Name">The name the programs use.</param>
    /// <param name="Provider">Its provider (discord, twitch), when the author had it.</param>
    public sealed record PackageConnection(string Name, string? Provider);

    /// <summary>A workflow to publish: the author's, as saved.</summary>
    public sealed record PackageSource(Guid Key, string Name, BlockProgram Program);

    /// <summary>
    /// What a package version holds: its workflows, calling each other by
    /// key, the connections they need and the package's error workflow. Fixed
    /// once published; its hash is the version's.
    /// </summary>
    /// <param name="ErrorWorkflow">The key of the workflow started when an automatic run fails; null: none.</param>
    public sealed record WorkflowPackageContent(IReadOnlyList<PackageWorkflow> Workflows, IReadOnlyList<PackageConnection> Connections,
        Guid? ErrorWorkflow = null)
    {
        /// <summary>
        /// SHA-256 of the workflows (key, name, main, program hash), connections
        /// and error workflow, in a fixed order (no error workflow: as before there was one).
        /// </summary>
        public string Hash => BlockProgramHash.Of(ErrorWorkflow is { } errorWorkflow
            ? JsonSerializer.SerializeToNode(new { workflows = HashedWorkflows, connections = HashedConnections, errorWorkflow },
                BlockParamsBinder.Options)
            : JsonSerializer.SerializeToNode(new { workflows = HashedWorkflows, connections = HashedConnections },
                BlockParamsBinder.Options));

        private IEnumerable<object> HashedWorkflows => Workflows.OrderBy(w => w.Key)
            .Select(w => new { key = w.Key, name = w.Name, main = w.Main, contentHash = w.ContentHash });

        private IEnumerable<PackageConnection> HashedConnections => Connections.OrderBy(c => c.Name, StringComparer.Ordinal);

        public string ToJson() => JsonSerializer.Serialize(this, BlockParamsBinder.Options);

        public static WorkflowPackageContent Parse(string json)
            => JsonSerializer.Deserialize<WorkflowPackageContent>(json, BlockParamsBinder.Options)
                ?? throw new FormatException("A package version holds nothing.");

        /// <summary>
        /// The content of the workflows chosen, webhook secrets removed. Fails
        /// (the problems, one per line) when a chosen program calls a workflow
        /// not chosen, or the error workflow is not chosen: the installer
        /// would get a link to nothing.
        /// </summary>
        /// <param name="main">The key of the one to open first; null: the first.</param>
        /// <param name="errorWorkflow">The package's error workflow (a key); null: none.</param>
        /// <param name="providerOf">The author's connection's provider by name, or null.</param>
        public static (WorkflowPackageContent? Content, IReadOnlyList<string> Problems) Build(
            IReadOnlyList<PackageSource> sources, Guid? main, Guid? errorWorkflow, Func<string, string?> providerOf)
        {
            var problems = new List<string>();
            if (sources.Count == 0)
                problems.Add("A package needs at least one workflow.");
            var keys = sources.Select(s => s.Key).ToHashSet();
            if (main is { } wanted && !keys.Contains(wanted))
                problems.Add($"The main workflow {wanted} is not chosen.");
            if (errorWorkflow is { } onError && !keys.Contains(onError))
                problems.Add("The package's error workflow is not chosen: choose it too.");

            var connections = new SortedSet<string>(StringComparer.Ordinal);
            var workflows = new List<PackageWorkflow>();
            var noSecrets = new LinkRewrite(new Dictionary<string, string>(), () => string.Empty);
            foreach (var source in sources)
            {
                var links = WorkflowLinks.Of(source.Program);
                foreach (var outside in links.Workflows.Where(key => !keys.Contains(key)))
                    problems.Add($"\"{source.Name}\" calls workflow {outside}, which is not chosen: choose it too.");
                connections.UnionWith(links.Connections);

                var program = WorkflowLinks.Rewrite(source.Program, noSecrets);
                workflows.Add(new PackageWorkflow(source.Key, source.Name, source.Key == (main ?? sources[0].Key),
                    BlockProgramHash.Of(program), program));
            }

            return problems.Count > 0
                ? (null, problems)
                : (new WorkflowPackageContent(workflows, connections.Select(c => new PackageConnection(c, providerOf(c))).ToList(),
                    errorWorkflow), []);
        }
    }
}
