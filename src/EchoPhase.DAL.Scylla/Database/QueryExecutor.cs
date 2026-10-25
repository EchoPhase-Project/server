// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using Cassandra;
using EchoPhase.DAL.Scylla.Interfaces;

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

    /// <summary>
    /// Runs CQL through the connection's <see cref="ICqlEngine"/>, logging each
    /// statement and its timing, and maps rows to results.
    /// </summary>
    public class QueryExecutor
    {
        public QueryExecutor(ICqlEngine engine, IQueryLogger? logger = null)
        {
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
            Logger = logger;
        }

        internal IQueryLogger? Logger { get; }

        internal ICqlEngine Engine { get; }

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
            => Project(Run(new CqlStatement(cql, parameters, idempotent), typeof(TResult)).Rows, projector);

        /// <summary>The whole result of a statement: its rows and, for an LWT, whether it applied.</summary>
        internal CqlResult Run(CqlStatement statement, Type? resultType = null)
            => Timed(statement, resultType, () => Engine.Execute(statement));

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
            var result = await RunAsync(new CqlStatement(cql, parameters, idempotent), typeof(TResult), ct);
            return Project(result.Rows, projector);
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

            var result = await RunAsync(new CqlStatement(cql, parameters, idempotent, pageSize, pagingState), typeof(TResult), ct);
            return new ScyllaPage<TResult>(Project(result.Rows, projector), result.PagingState);
        }

        /// <inheritdoc cref="Run"/>
        internal async Task<CqlResult> RunAsync(CqlStatement statement, Type? resultType, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            try
            {
                Logger?.LogQuery(statement.Cql, resultType ?? typeof(Row), statement.Values!);
                var result = await Engine.ExecuteAsync(statement, ct);
                Logger?.LogQueryTiming(stopwatch.ElapsedMilliseconds);
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger?.LogError(statement.Cql, ex);
                throw;
            }
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

        #endregion

        #region Batches

        internal void ExecuteBatch(IReadOnlyList<CqlStatement> statements, BatchType type)
            => Timed(new CqlStatement($"BATCH ({statements.Count})", []), null, () =>
            {
                Engine.ExecuteBatch(statements, type);
                return CqlResult.Empty;
            });

        internal async Task ExecuteBatchAsync(IReadOnlyList<CqlStatement> statements, BatchType type, CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();
            await Engine.ExecuteBatchAsync(statements, type, ct);
            Logger?.LogQueryTiming(stopwatch.ElapsedMilliseconds);
        }

        #endregion

        #region Results

        private CqlResult Timed(CqlStatement statement, Type? resultType, Func<CqlResult> run)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                Logger?.LogQuery(statement.Cql, resultType ?? typeof(Row), statement.Values!);
                var result = run();
                Logger?.LogQueryTiming(stopwatch.ElapsedMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                Logger?.LogError(statement.Cql, ex);
                throw;
            }
        }

        private static List<TResult> Project<TResult>(IReadOnlyList<Row> rows, Func<Row, TResult>? projector)
        {
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
                return default!;

            var value = row.GetValue<object>(0);
            if (value is TResult typedValue)
                return typedValue;

            return (TResult)Convert.ChangeType(value, typeof(TResult));
        }

        #endregion
    }
}
