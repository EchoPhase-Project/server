// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Files
{
    /// <summary>What files tell their owner about (the host sends notifications).</summary>
    public interface IFileEvents
    {
        /// <summary>An upload took the owner's files past <see cref="FileService.QuotaWarningPercent"/> of the quota.</summary>
        Task QuotaNearlyFullAsync(Guid ownerId, int percent, CancellationToken ct = default);
    }
}
