// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Entities
{
    /// <summary>
    /// A run of a workflow, by workflow: the index a workflow's run history is
    /// read from (block_runs is by user). Written once with the run, expiring
    /// with it; the run itself is read from block_runs.
    /// </summary>
    public class WorkflowRun
    {
        public Guid WorkflowId { get; set; }
        public Guid RunId { get; set; }
        public Guid UserId { get; set; }
    }
}
