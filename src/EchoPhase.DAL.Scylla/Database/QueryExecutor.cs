// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Diagnostics;
using Cassandra;
using EchoPhase.DAL.Scylla.Interfaces;
using ISession = Cassandra.ISession;

namespace EchoPhase.DAL.Scylla.Database
{
    /// <summary>One page of results and the cursor to the next one.</summary>
    /// <param name="NextPagingState">
    /// Opaque driver cursor; <c>null</c> on the last page. Hand it back unchanged.
    /// </param>
    public sealed record ScyllaPage<T>(IReadOnlyList<T> Items, byte[]? NextPagingState)
    {
        public bool HasMore => NextPagingState is not null;
    }

    public class QueryExecutor
    {
        private readonly ISession _session;
        private readonly ConcurrentDictionary<string, PreparedStatement> _preparedStatements;

        public QueryExecutor(ISession session, IQueryLogger? logger = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            Logger = logger;
            _preparedStatements = new ConcurrentDictionary<string, PreparedStatement>();
        }

        internal IQueryLogger? Logger { get; }

        #region Sync

        public IEnumerable<TResult> Execute<TResult>(
            string cql,
            Func<Row, TResult>? projector = null,
            params object[] parameters)
            => Execute(cql, projector, parameters, idempotent: false);

        /// <param name="idempotent">
        /// Safe to run twice (plain SELECT/INSERT/UPDATE/DELETE, no LWT or
        /// counters). The driver only retries idempotent statements on timeouts.
        /// </param>
        internal IEnumerable<TResult> Execute<TResult>(
            string cql,
            Func<Row, TResult>? projector,
            object[] parameters,
            bool idempotent)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                Logger?.LogQuery(cql, typeof(TResult), parameters);

                var rowSet = _session.Execute(CreateStatement(cql, parameters, idempotent));

                stopwatch.Stop();
                Logger?.LogQueryTiming(stopwatch.ElapsedMilliseconds);

                return ProjectResults<TResult>(rowSet, projector, materializeAllPages: true);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger?.LogError(cql, ex);
                throw;
            }
        }

        public IEnumerable<Row> Execute(string cql, params object[] parameters)
            => Execute<Row>(cql, null, parameters);

        public TResult? ExecuteScalar<TResult>(string cql, params object[] parameters)
            => Execute<TResult>(cql, row => MapScalar<TResult>(row), parameters).FirstOrDefault();

        public void ExecuteNonQuery(string cql, params object[] parameters)
            => Execute<Row>(cql, null, parameters);

        internal void ExecuteNonQuery(string cql, object[] parameters, bool idempotent)
            => Execute<Row>(cql, null, parameters, idempotent);

        #endregion

        #region Async

        public Task<IEnumerable<TResult>> ExecuteAsync<TResult>(
            string cql,
            Func<Row, TResult>? projector = null,
            params object[] parameters)
            => ExecuteAsync(cql, projector, parameters, CancellationToken.None);

        public Task<IEnumerable<TResult>> ExecuteAsync<TResult>(
            string cql,
            Func<Row, TResult>? projector,
            object[] parameters,
            CancellationToken ct)
            => ExecuteAsync(cql, projector, parameters, idempotent: false, ct);

        internal async Task<IEnumerable<TResult>> ExecuteAsync<TResult>(
            string cql,
            Func<Row, TResult>? projector,
            object[] parameters,
            bool idempotent,
            CancellationToken ct)
        {
            var rowSet = await RunAsync(cql, typeof(TResult), parameters, idempotent, pageSize: null, pagingState: null, ct);
            return ProjectResults<TResult>(rowSet, projector, materializeAllPages: true);
        }

        /// <summary>
        /// One page of <paramref name="pageSize"/> rows starting at
        /// <paramref name="pagingState"/> (<c>null</c> for the first page).
        /// Memory stays bounded however large the partition is.
        /// </summary>
        internal async Task<ScyllaPage<TResult>> ExecutePageAsync<TResult>(
            string cql,
            Func<Row, TResult>? projector,
            object[] parameters,
            int pageSize,
            byte[]? pagingState,
            bool idempotent,
            CancellationToken ct)
        {
            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be positive.");

            var rowSet = await RunAsync(cql, typeof(TResult), parameters, idempotent, pageSize, pagingState, ct);
            var items = ProjectResults<TResult>(rowSet, projector, materializeAllPages: false);
            return new ScyllaPage<TResult>(items, rowSet.PagingState);
        }

        public Task<IEnumerable<Row>> ExecuteAsync(string cql, params object[] parameters)
            => ExecuteAsync<Row>(cql, null, parameters);

        public Task<IEnumerable<Row>> ExecuteAsync(string cql, object[] parameters, CancellationToken ct)
            => ExecuteAsync<Row>(cql, null, parameters, ct);

        public Task<TResult?> ExecuteScalarAsync<TResult>(string cql, params object[] parameters)
            => ExecuteScalarAsync<TResult>(cql, parameters, CancellationToken.None);

        public async Task<TResult?> ExecuteScalarAsync<TResult>(string cql, object[] parameters, CancellationToken ct)
        {
            var result = await ExecuteAsync<TResult>(cql, row => MapScalar<TResult>(row), parameters, ct);
            return result.FirstOrDefault();
        }

        public Task ExecuteNonQueryAsync(string cql, params object[] parameters)
            => ExecuteNonQueryAsync(cql, parameters, CancellationToken.None);

        public Task ExecuteNonQueryAsync(string cql, object[] parameters, CancellationToken ct)
            => ExecuteAsync<Row>(cql, null, parameters, ct);

        internal Task ExecuteNonQueryAsync(string cql, object[] parameters, bool idempotent, CancellationToken ct)
            => ExecuteAsync<Row>(cql, null, parameters, idempotent, ct);

        private async Task<RowSet> RunAsync(
            string cql,
            Type resultType,
            object[] parameters,
            bool idempotent,
            int? pageSize,
            byte[]? pagingState,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                Logger?.LogQuery(cql, resultType, parameters);

                var statement = parameters.Length > 0
                    ? await CreateBoundStatementAsync(cql, parameters, idempotent, ct)
                    : new SimpleStatement(cql).SetIdempotence(idempotent);

                if (pageSize is { } size)
                    statement.SetPageSize(size).SetAutoPage(false).SetPagingState(pagingState);

                // Driver 3.x has no cancellable ExecuteAsync: WaitAsync releases the
                // caller on cancellation; the server-side request still completes.
                var rowSet = await _session.ExecuteAsync(statement).WaitAsync(ct);

                stopwatch.Stop();
                Logger?.LogQueryTiming(stopwatch.ElapsedMilliseconds);

                return rowSet;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                stopwatch.Stop();
                Logger?.LogError(cql, ex);
                throw;
            }
        }

        #endregion

        #region Statements

        private IStatement CreateStatement(string cql, object[] parameters, bool idempotent)
            => parameters.Length > 0
                ? Prepare(cql).Bind(parameters).SetIdempotence(idempotent)
                : new SimpleStatement(cql).SetIdempotence(idempotent);

        private async Task<IStatement> CreateBoundStatementAsync(
            string cql, object[] parameters, bool idempotent, CancellationToken ct)
            => (await PrepareAsync(cql, ct)).Bind(parameters).SetIdempotence(idempotent);

        /// <summary>
        /// Prepared statement for <paramref name="cql"/>, shared by everything on
        /// this connection (queries and batches alike).
        /// </summary>
        internal PreparedStatement Prepare(string cql)
            => _preparedStatements.GetOrAdd(cql, _session.Prepare);

        internal async Task<PreparedStatement> PrepareAsync(string cql, CancellationToken ct = default)
        {
            if (_preparedStatements.TryGetValue(cql, out var prepared))
                return prepared;

            prepared = await _session.PrepareAsync(cql).WaitAsync(ct);
            return _preparedStatements.GetOrAdd(cql, prepared);
        }

        #endregion

        #region Results

        private static List<TResult> ProjectResults<TResult>(
            RowSet rowSet, Func<Row, TResult>? projector, bool materializeAllPages)
        {
            // With auto-paging on, enumerating a RowSet fetches further pages;
            // for a single page only the rows already received are read.
            IEnumerable<Row> rows = materializeAllPages
                ? rowSet
                : rowSet.Take(rowSet.GetAvailableWithoutFetching());

            if (projector != null)
                return rows.Select(projector).ToList();

            if (typeof(TResult) == typeof(Row))
                return rows.Cast<TResult>().ToList();

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

        #endregion

        public void ClearPreparedStatements()
        {
            _preparedStatements.Clear();
        }
    }
}
