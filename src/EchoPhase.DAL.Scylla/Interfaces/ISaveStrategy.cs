// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Interfaces
{
    public interface ISaveStrategy
    {
        Task<int> ExecuteAsync(IEnumerable<TrackedEntity> entities, Transaction? transaction);
        int Execute(IEnumerable<TrackedEntity> entities, Transaction? transaction);
    }
}
