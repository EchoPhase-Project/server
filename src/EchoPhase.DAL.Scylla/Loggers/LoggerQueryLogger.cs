// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Interfaces;
using Microsoft.Extensions.Logging;

namespace EchoPhase.DAL.Scylla.Loggers
{
    /// <summary>
    /// Routes query logging through <see cref="ILogger"/>, so levels, filters
    /// and sinks apply. Bound values are never logged: they may hold user data
    /// or secrets; only their count is.
    /// </summary>
    public class LoggerQueryLogger : IQueryLogger
    {
        private readonly ILogger _logger;

        public LoggerQueryLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void LogQuery(string cql, Type resultType, object[] parameters)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Executing CQL returning {ResultType} with {ParameterCount} parameter(s): {Cql}",
                    resultType.Name, parameters.Length, cql);
        }

        public void LogQueryTiming(long elapsedMs)
            => _logger.LogDebug("CQL executed in {ElapsedMs} ms", elapsedMs);

        public void LogError(string cql, Exception ex)
            => _logger.LogWarning(ex, "CQL failed: {Cql}", cql);

        public void LogWarning(string message)
            => _logger.LogWarning("{ScyllaWarning}", message);
    }
}
