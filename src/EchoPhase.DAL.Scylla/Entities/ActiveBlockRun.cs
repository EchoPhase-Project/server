// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Entities
{
    /// <summary>
    /// A queued or running run. Rows are deleted when the run finishes, so the
    /// table stays small and can be scanned on startup to mark runs that a
    /// restart interrupted.
    /// </summary>
    public class ActiveBlockRun
    {
        public Guid RunId { get; set; }
        public Guid UserId { get; set; }
    }
}
