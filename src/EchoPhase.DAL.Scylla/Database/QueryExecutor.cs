// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Diagnostics;
using Cassandra;
using EchoPhase.DAL.Scylla.Interfaces;
using ISession = Cassandra.ISession;

namespace EchoPhase.DAL.Scylla.Database
{
    public class QueryExecutor
    {
        private readonly ISession _session;
        private readonly IQueryLogger? _logger;
        private readonly ConcurrentDictionary<string, PreparedStatement> _preparedStatements;

        public QueryExecutor(ISession session, IQueryLogger? logger = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _logger = logger;
            _preparedStatements = new ConcurrentDictionary<string, PreparedStatement>();
        }

        public IEnumerable<TResult> Execute<TResult>(
            string cql,
            Func<Row, TResult>? projector = null,
            params object[] parameters)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger?.LogQuery(cql, typeof(TResult), parameters);

                var rowSet = parameters.Length > 0
                    ? _session.Execute(GetBoundStatement(cql, parameters))
                    : _session.Execute(cql);

                stopwatch.Stop();
                _logger?.LogQueryTiming(stopwatch.ElapsedMilliseconds);

                return ProjectResults<TResult>(rowSet, projector);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger?.LogError(cql, ex);
                throw;
            }
        }

        public async Task<IEnumerable<TResult>> ExecuteAsync<TResult>(
            string cql,
            Func<Row, TResult>? projector = null,
            params object[] parameters)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger?.LogQuery(cql, typeof(TResult), parameters);

                var rowSet = parameters.Length > 0
                    ? await _session.ExecuteAsync(await GetBoundStatementAsync(cql, parameters))
                    : await _session.ExecuteAsync(new SimpleStatement(cql));

                stopwatch.Stop();
                _logger?.LogQueryTiming(stopwatch.ElapsedMilliseconds);

                return ProjectResults<TResult>(rowSet, projector);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger?.LogError(cql, ex);
                throw;
            }
        }

        // Convenience methods
        public IEnumerable<Row> Execute(string cql, params object[] parameters)
            => Execute<Row>(cql, null, parameters);

        public Task<IEnumerable<Row>> ExecuteAsync(string cql, params object[] parameters)
            => ExecuteAsync<Row>(cql, null, parameters);

        public TResult? ExecuteScalar<TResult>(string cql, params object[] parameters)
        {
            var result = Execute<TResult>(cql, row => MapScalar<TResult>(row), parameters);
            return result.FirstOrDefault();
        }

        public async Task<TResult?> ExecuteScalarAsync<TResult>(string cql, params object[] parameters)
        {
            var result = await ExecuteAsync<TResult>(cql, row => MapScalar<TResult>(row), parameters);
            return result.FirstOrDefault();
        }

        public void ExecuteNonQuery(string cql, params object[] parameters)
        {
            Execute<Row>(cql, null, parameters);
        }

        public async Task ExecuteNonQueryAsync(string cql, params object[] parameters)
        {
            await ExecuteAsync<Row>(cql, null, parameters);
        }

        private BoundStatement GetBoundStatement(string cql, object[] parameters)
        {
            var prepared = _preparedStatements.GetOrAdd(cql, _session.Prepare);
            return prepared.Bind(parameters);
        }

        private async Task<BoundStatement> GetBoundStatementAsync(string cql, object[] parameters)
        {
            if (!_preparedStatements.TryGetValue(cql, out var prepared))
            {
                prepared = await _session.PrepareAsync(cql);
                _preparedStatements.TryAdd(cql, prepared);
            }
            return prepared.Bind(parameters);
        }

        private static IEnumerable<TResult> ProjectResults<TResult>(RowSet rowSet, Func<Row, TResult>? projector)
        {
            if (projector != null)
            {
                return rowSet.Select(projector).ToList();
            }

            if (typeof(TResult) == typeof(Row))
            {
                return rowSet.Cast<TResult>().ToList();
            }

            throw new InvalidOperationException(
                $"No projector provided for type {typeof(TResult).Name}. " +
                "Either provide a projector function or use Row as the result type.");
        }

        private static TResult MapScalar<TResult>(Row row)
        {
            if (row == null || row.GetValue<object>(0) == null)
            {
                return default!;
            }

            var value = row.GetValue<object>(0);

            if (value is TResult typedValue)
            {
                return typedValue;
            }

            return (TResult)Convert.ChangeType(value, typeof(TResult));
        }

        public void ClearPreparedStatements()
        {
            _preparedStatements.Clear();
        }
    }
}
