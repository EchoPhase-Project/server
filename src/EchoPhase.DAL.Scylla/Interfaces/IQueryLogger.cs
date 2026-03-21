// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Interfaces
{
    public interface IQueryLogger
    {
        void LogQuery(string cql, Type resultType, object[] parameters);
        void LogQueryTiming(long elapsedMs);
        void LogError(string cql, Exception ex);
    }
}
