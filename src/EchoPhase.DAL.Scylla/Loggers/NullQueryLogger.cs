// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla.Loggers
{
    public sealed class NullQueryLogger : IQueryLogger
    {
        public static readonly NullQueryLogger Instance = new();

        public void LogQuery(string cql, Type resultType, object[] parameters) { }

        public void LogQueryTiming(long elapsedMs) { }

        public void LogError(string cql, Exception ex) { }
    }
}
