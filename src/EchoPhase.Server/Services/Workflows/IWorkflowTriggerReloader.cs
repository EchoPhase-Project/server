// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Server.Services.Workflows
{
    /// <summary>What listens for a user's workflow triggers, told when their workflows change.</summary>
    public interface IWorkflowTriggerReloader
    {
        Task ReloadUserAsync(Guid userId, CancellationToken ct = default);
    }
}
