// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Entities
{
    /// <summary>
    /// A run of one of a package's workflows, by package: the index the
    /// package's run history is read from. Written once with the run, expiring
    /// with it; the run itself is read from block_runs.
    /// </summary>
    public class PackageRun
    {
        public Guid PackageId { get; set; }
        public Guid RunId { get; set; }
        public Guid UserId { get; set; }
    }
}
