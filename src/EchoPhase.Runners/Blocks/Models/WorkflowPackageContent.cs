// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Runners.Blocks.Handlers;

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>One workflow of a package version.</summary>
    /// <param name="Key">
    /// The author's workflow it was published from: the same in every version,
    /// so an update finds the copy it replaces; the package's programs refer
    /// to each other by it.
    /// </param>
    /// <param name="Main">The one to open first.</param>
    /// <param name="ContentHash">Its program's hash, as published (webhook secrets removed).</param>
    public sealed record PackageWorkflow(Guid Key, string Name, bool Main, string ContentHash, BlockProgram Program);

    /// <summary>A connection the package's programs use: the installer names one of theirs for it.</summary>
    /// <param name="Name">The name the programs use.</param>
    /// <param name="Provider">Its provider (discord, twitch), when the author had it.</param>
    public sealed record PackageConnection(string Name, string? Provider);

    /// <summary>A workflow to publish: the author's, as saved.</summary>
    public sealed record PackageSource(Guid Id, string Name, BlockProgram Program);

    /// <summary>
    /// What a package version holds: its workflows, pointing at each other,
    /// and the connections they need. Fixed once published; its hash is the
    /// version's.
    /// </summary>
    public sealed record WorkflowPackageContent(IReadOnlyList<PackageWorkflow> Workflows, IReadOnlyList<PackageConnection> Connections)
    {
        /// <summary>SHA-256 of the workflows (key, name, main, program hash) and connections, in a fixed order.</summary>
        public string Hash => BlockProgramHash.Of(JsonSerializer.SerializeToNode(new
        {
            workflows = Workflows.OrderBy(w => w.Key)
                .Select(w => new { key = w.Key, name = w.Name, main = w.Main, contentHash = w.ContentHash }),
            connections = Connections.OrderBy(c => c.Name, StringComparer.Ordinal),
        }, BlockParamsBinder.Options));

        public string ToJson() => JsonSerializer.Serialize(this, BlockParamsBinder.Options);

        public static WorkflowPackageContent Parse(string json)
            => JsonSerializer.Deserialize<WorkflowPackageContent>(json, BlockParamsBinder.Options)
                ?? throw new FormatException("A package version holds nothing.");

        /// <summary>
        /// The content of the workflows given, webhook secrets removed. Fails
        /// (the problems, one per line) when a program names a workflow not
        /// among them: the installer would get a link to nothing.
        /// </summary>
        /// <param name="providerOf">The author's connection's provider by name, or null.</param>
        public static (WorkflowPackageContent? Content, IReadOnlyList<string> Problems) Build(
            IReadOnlyList<PackageSource> sources, Guid? main, Func<string, string?> providerOf)
        {
            var problems = new List<string>();
            if (sources.Count == 0)
                problems.Add("A package needs at least one workflow.");
            var ids = sources.Select(s => s.Id).ToHashSet();
            if (main is { } wanted && !ids.Contains(wanted))
                problems.Add($"The main workflow {wanted} is not in the package.");

            var connections = new SortedSet<string>(StringComparer.Ordinal);
            var workflows = new List<PackageWorkflow>();
            var noSecrets = new LinkRewrite(new Dictionary<Guid, Guid>(), new Dictionary<string, string>(), () => string.Empty);
            foreach (var source in sources)
            {
                var links = WorkflowLinks.Of(source.Program);
                foreach (var outside in links.Workflows.Where(id => !ids.Contains(id)))
                    problems.Add($"\"{source.Name}\" uses workflow {outside}, which is not in the package: add it.");
                connections.UnionWith(links.Connections);

                var program = WorkflowLinks.Rewrite(source.Program, noSecrets);
                workflows.Add(new PackageWorkflow(source.Id, source.Name, source.Id == (main ?? sources[0].Id),
                    BlockProgramHash.Of(program), program));
            }

            return problems.Count > 0
                ? (null, problems)
                : (new WorkflowPackageContent(workflows, connections.Select(c => new PackageConnection(c, providerOf(c))).ToList()), []);
        }
    }
}
