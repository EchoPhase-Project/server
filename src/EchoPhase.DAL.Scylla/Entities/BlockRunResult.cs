// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Entities
{
    /// <summary>
    /// A finished run's output, variables and errors as JSON. Kept much
    /// shorter than <see cref="BlockRun"/>: the history outlives the details.
    /// </summary>
    public class BlockRunResult
    {
        public Guid UserId { get; set; }
        public Guid RunId { get; set; }
        public string Payload { get; set; } = string.Empty;
    }
}
